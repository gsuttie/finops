using System.Text.Json;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using FinOps.Models;

namespace FinOps.Services;

public class SecurityRecommendationService(TenantClientManager tenantClientManager) : ISecurityRecommendationService
{
    private const string SecurityQuery = """
        securityresources
        | where type == "microsoft.security/assessments"
        | where properties.status.code == "Unhealthy"
        | extend
            assessmentName = tostring(properties.displayName),
            severity = tostring(properties.status.severity),
            statusCode = tostring(properties.status.code),
            category = tostring(properties.metadata.categories[0]),
            description = tostring(properties.metadata.description),
            resourceId = tostring(properties.resourceDetails.Id)
        | extend
            resourceName = tostring(split(resourceId, "/")[-1]),
            resourceType = tostring(strcat(split(resourceId, "/")[-3], "/", split(resourceId, "/")[-2])),
            resourceGroup = tostring(split(resourceId, "/")[4])
        | project id, assessmentName, severity, statusCode, category, description,
                  resourceId, resourceName, resourceType, resourceGroup, subscriptionId
        """;

    public async Task<IReadOnlyList<SecurityRecommendation>> GetSecurityRecommendationsAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subList = subscriptions.ToList();
        if (subList.Count == 0)
            return [];

        var subLookup = subList.ToDictionary(s => s.SubscriptionId, s => s);
        var tenantGroups = subList.GroupBy(s => s.TenantId);

        var tenantTasks = tenantGroups.Select(group => ProcessTenantAsync(group.Key, group.ToList(), subLookup));
        var tenantResults = await Task.WhenAll(tenantTasks);

        return tenantResults.SelectMany(r => r).ToList();
    }

    private async Task<List<SecurityRecommendation>> ProcessTenantAsync(
        string tenantId,
        List<TenantSubscription> subscriptions,
        Dictionary<string, TenantSubscription> subLookup)
    {
        var client = tenantClientManager.GetClientForTenant(tenantId);
        var tenant = client.GetTenants().First();
        var subscriptionIds = subscriptions.Select(s => s.SubscriptionId).ToList();

        var content = new ResourceQueryContent(SecurityQuery)
        {
            Options = new ResourceQueryRequestOptions { ResultFormat = ResultFormat.ObjectArray }
        };
        foreach (var subId in subscriptionIds)
        {
            content.Subscriptions.Add(subId);
        }

        var results = new List<SecurityRecommendation>();
        var response = await tenant.GetResourcesAsync(content);
        var jsonElement = response.Value.Data.ToObjectFromJson<JsonElement>();

        if (jsonElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in jsonElement.EnumerateArray())
            {
                var subscriptionId = row.GetProperty("subscriptionId").GetString() ?? "";
                var sub = subLookup.GetValueOrDefault(subscriptionId);

                results.Add(new SecurityRecommendation
                {
                    AssessmentId = row.GetProperty("id").GetString() ?? "",
                    RecommendationName = row.GetProperty("assessmentName").GetString() ?? "",
                    Severity = row.GetProperty("severity").GetString() ?? "",
                    Status = row.GetProperty("statusCode").GetString() ?? "",
                    Category = row.GetProperty("category").GetString() ?? "",
                    Description = row.GetProperty("description").GetString() ?? "",
                    ResourceId = row.GetProperty("resourceId").GetString() ?? "",
                    ResourceName = row.GetProperty("resourceName").GetString() ?? "",
                    ResourceType = row.GetProperty("resourceType").GetString() ?? "",
                    ResourceGroup = row.GetProperty("resourceGroup").GetString() ?? "",
                    SubscriptionName = sub?.DisplayName ?? subscriptionId,
                    SubscriptionId = subscriptionId,
                    TenantId = sub?.TenantId ?? tenantId
                });
            }
        }

        return results;
    }
}
