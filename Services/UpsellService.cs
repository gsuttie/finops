using FinOps.Models;

namespace FinOps.Services;

public class UpsellService(
    ISecurityRecommendationService securityService,
    IAdvisorService advisorService,
    IRightsizingService rightsizingService,
    IOrphanedResourceService orphanedResourceService,
    IServiceRetirementService serviceRetirementService,
    IPrivateEndpointService privateEndpointService,
    ILogAnalyticsService logAnalyticsService) : IUpsellService
{
    public async Task<IReadOnlyList<UpsellOpportunity>> GetOpportunitiesAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subList = subscriptions.ToList();

        var secTask = securityService.GetSecurityRecommendationsAsync(subList);
        var advTask = advisorService.GetRecommendationsAsync(subList);
        var rsTask = rightsizingService.GetRecommendationsAsync(subList);
        var orpTask = orphanedResourceService.ScanForOrphanedResourcesAsync(subList);
        var retTask = serviceRetirementService.GetServiceRetirementsAsync(subList);
        var peTask = privateEndpointService.GetPrivateEndpointRecommendationsAsync(subList);
        var laTask = logAnalyticsService.GetWorkspacesAsync(subList);

        await Task.WhenAll(secTask, advTask, rsTask, orpTask, retTask, peTask, laTask);

        var results = new List<UpsellOpportunity>();

        results.AddRange(TryMapSecurity(secTask));
        results.AddRange(TryMapAdvisor(advTask));
        results.AddRange(TryMapRightsizing(rsTask));
        results.AddRange(TryMapOrphaned(orpTask));
        results.AddRange(TryMapRetirements(retTask));
        results.AddRange(TryMapPrivateEndpoints(peTask));
        results.AddRange(TryMapLogAnalytics(laTask));

        return results
            .OrderBy(o => o.Impact == UpsellImpact.High ? 0 : o.Impact == UpsellImpact.Medium ? 1 : 2)
            .ThenBy(o => o.Category.ToString())
            .ToList();
    }

    private static IEnumerable<UpsellOpportunity> TryMapSecurity(Task<IReadOnlyList<SecurityRecommendation>> task)
    {
        if (task.IsFaulted) return [];
        return task.Result.Select(r => new UpsellOpportunity
        {
            Id = $"sec-{r.AssessmentId}",
            Title = r.RecommendationName,
            Category = UpsellCategory.Security,
            Impact = r.Severity switch
            {
                "High" => UpsellImpact.High,
                "Medium" => UpsellImpact.Medium,
                _ => UpsellImpact.Low
            },
            BusinessValue = $"Resolving this {r.Severity.ToLower()}-severity security finding reduces breach risk and strengthens the customer's security posture in Microsoft Defender.",
            TechnicalDetail = r.Description,
            ResourceName = r.ResourceName,
            ResourceId = r.ResourceId,
            SubscriptionName = r.SubscriptionName,
            SubscriptionId = r.SubscriptionId,
            TenantId = r.TenantId,
            Source = "Security",
            AzurePortalUrl = $"https://portal.azure.com/#@{r.TenantId}/resource{r.ResourceId}"
        });
    }

    private static IEnumerable<UpsellOpportunity> TryMapAdvisor(Task<IReadOnlyList<AdvisorRecommendation>> task)
    {
        if (task.IsFaulted) return [];
        return task.Result.Select(r => new UpsellOpportunity
        {
            Id = $"adv-{r.RecommendationId}",
            Title = r.Name,
            Category = r.Category switch
            {
                "Cost" => UpsellCategory.CostOptimization,
                "Security" => UpsellCategory.Security,
                "HighAvailability" => UpsellCategory.Reliability,
                "OperationalExcellence" => UpsellCategory.Governance,
                "Performance" => UpsellCategory.Modernization,
                _ => UpsellCategory.Governance
            },
            Impact = r.Impact switch
            {
                "High" => UpsellImpact.High,
                "Medium" => UpsellImpact.Medium,
                _ => UpsellImpact.Low
            },
            BusinessValue = $"Azure Advisor recommends: {r.Solution}",
            TechnicalDetail = r.Problem,
            ResourceName = r.ImpactedValue,
            ResourceId = r.ResourceId,
            SubscriptionName = r.SubscriptionName,
            SubscriptionId = r.SubscriptionId,
            TenantId = r.TenantId,
            Source = "Advisor",
            AzurePortalUrl = !string.IsNullOrEmpty(r.ResourceId)
                ? $"https://portal.azure.com/#@{r.TenantId}/resource{r.ResourceId}"
                : null
        });
    }

    private static IEnumerable<UpsellOpportunity> TryMapRightsizing(Task<IReadOnlyList<RightsizingRecommendation>> task)
    {
        if (task.IsFaulted) return [];
        return task.Result.Select(r => new UpsellOpportunity
        {
            Id = $"rs-{r.ResourceId}",
            Title = r.RecommendationTitle,
            Category = UpsellCategory.CostOptimization,
            Impact = r.ImpactLevel switch
            {
                "High" => UpsellImpact.High,
                "Medium" => UpsellImpact.Medium,
                _ => UpsellImpact.Low
            },
            BusinessValue = r.PotentialSavingsMonthly.HasValue
                ? $"Rightsizing this resource could save approximately ${r.PotentialSavingsMonthly:F0}/month. {r.RecommendedAction}"
                : $"Rightsizing this resource will reduce compute costs. {r.RecommendedAction}",
            TechnicalDetail = $"Current SKU: {r.CurrentSku ?? "Unknown"}. Recommended action: {r.RecommendedAction}",
            ResourceName = r.ResourceName,
            ResourceId = r.ResourceId,
            SubscriptionName = r.SubscriptionName,
            SubscriptionId = r.SubscriptionId,
            TenantId = r.TenantId,
            Source = "Rightsizing",
            AzurePortalUrl = $"https://portal.azure.com/#@{r.TenantId}/resource{r.ResourceId}",
            EstimatedMonthlySavings = r.PotentialSavingsMonthly,
        });
    }

    private static IEnumerable<UpsellOpportunity> TryMapOrphaned(Task<IReadOnlyList<OrphanedResourceInfo>> task)
    {
        if (task.IsFaulted) return [];
        return task.Result.Select(r => new UpsellOpportunity
        {
            Id = $"orp-{r.ResourceId}",
            Title = $"Orphaned {r.ResourceType}: {r.Name}",
            Category = UpsellCategory.CostOptimization,
            Impact = UpsellImpact.Medium,
            BusinessValue = "This unused resource is incurring unnecessary costs. Removing it will reduce the monthly bill with no impact on workloads.",
            TechnicalDetail = $"Resource type: {r.ResourceType}. Category: {r.Category}. Resource group: {r.ResourceGroup}.",
            ResourceName = r.Name,
            ResourceId = r.ResourceId,
            SubscriptionName = r.SubscriptionName,
            SubscriptionId = r.SubscriptionId,
            TenantId = r.TenantId,
            Source = "Orphaned Resources",
            AzurePortalUrl = $"https://portal.azure.com/#@{r.TenantId}/resource{r.ResourceId}"
        });
    }

    private static IEnumerable<UpsellOpportunity> TryMapRetirements(Task<IReadOnlyList<ServiceRetirement>> task)
    {
        if (task.IsFaulted) return [];
        var results = new List<UpsellOpportunity>();
        foreach (var r in task.Result)
        {
            var impact = UpsellImpact.Low;
            if (DateTimeOffset.TryParse(r.RetirementDate, out var retDate))
            {
                var daysUntil = (retDate - DateTimeOffset.UtcNow).TotalDays;
                impact = daysUntil < 90 ? UpsellImpact.High : daysUntil < 180 ? UpsellImpact.Medium : UpsellImpact.Low;
            }

            results.Add(new UpsellOpportunity
            {
                Id = $"ret-{r.SubscriptionId}-{r.ServiceName}",
                Title = $"Service Retirement: {r.RetiringFeature}",
                Category = UpsellCategory.Modernization,
                Impact = impact,
                BusinessValue = $"{r.ServiceName} is retiring on {r.RetirementDate}. Migrating now avoids service disruption and opens modernization opportunities.",
                TechnicalDetail = r.Description,
                ResourceName = r.ServiceName,
                SubscriptionName = r.SubscriptionName,
                SubscriptionId = r.SubscriptionId,
                TenantId = r.TenantId,
                Source = "Service Retirements"
            });
        }
        return results;
    }

    private static IEnumerable<UpsellOpportunity> TryMapPrivateEndpoints(Task<IReadOnlyList<PrivateEndpointRecommendation>> task)
    {
        if (task.IsFaulted) return [];
        return task.Result.Select(r => new UpsellOpportunity
        {
            Id = $"pe-{r.ResourceId}",
            Title = $"Enable Private Endpoint: {r.ResourceName}",
            Category = UpsellCategory.Security,
            Impact = r.HasPublicAccess ? UpsellImpact.High : UpsellImpact.Medium,
            BusinessValue = r.HasPublicAccess
                ? "This resource is publicly accessible. Adding a private endpoint eliminates public internet exposure, significantly reducing attack surface."
                : "Adding a private endpoint improves network isolation and aligns with Zero Trust security principles.",
            TechnicalDetail = r.RecommendationReason,
            ResourceName = r.ResourceName,
            ResourceId = r.ResourceId,
            SubscriptionName = r.SubscriptionName,
            SubscriptionId = r.SubscriptionId,
            TenantId = r.TenantId,
            Source = "Private Endpoints",
            AzurePortalUrl = $"https://portal.azure.com/#@{r.TenantId}/resource{r.ResourceId}"
        });
    }

    private static IEnumerable<UpsellOpportunity> TryMapLogAnalytics(Task<IReadOnlyList<WorkspaceInfo>> task)
    {
        if (task.IsFaulted) return [];
        return task.Result
            .Where(w => w.RetentionInDays < 30)
            .Select(w => new UpsellOpportunity
            {
                Id = $"la-{w.WorkspaceResourceId}",
                Title = $"Low Log Retention: {w.Name}",
                Category = UpsellCategory.Governance,
                Impact = UpsellImpact.Medium,
                BusinessValue = $"This Log Analytics workspace retains data for only {w.RetentionInDays} days, which may be insufficient for compliance, auditing, or incident investigation requirements.",
                TechnicalDetail = $"Workspace: {w.Name}. SKU: {w.SkuName}. Current retention: {w.RetentionInDays} days. Resource group: {w.ResourceGroup}.",
                ResourceName = w.Name,
                ResourceId = w.WorkspaceResourceId,
                SubscriptionName = w.SubscriptionName,
                SubscriptionId = w.SubscriptionId,
                TenantId = w.TenantId,
                Source = "Log Analytics",
                AzurePortalUrl = $"https://portal.azure.com/#@{w.TenantId}/resource{w.WorkspaceResourceId}"
            });
    }
}
