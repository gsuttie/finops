using System.Text.Json;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using FinOps.Models;

namespace FinOps.Services;

public class RightsizingService(
    TenantClientManager tenantClientManager,
    ILogger<RightsizingService> logger) : IRightsizingService
{
    public async Task<IReadOnlyList<RightsizingRecommendation>> GetRecommendationsAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subList = subscriptions.ToList();
        if (subList.Count == 0)
            return [];

        logger.LogInformation("Scanning {Count} subscriptions for rightsizing recommendations", subList.Count);

        var subLookup = subList.ToDictionary(s => s.SubscriptionId, s => s);
        var tenantGroups = subList.GroupBy(s => s.TenantId);

        var tenantTasks = tenantGroups.Select(group =>
            ProcessTenantAsync(group.Key, group.ToList(), subLookup));

        var tenantResults = await Task.WhenAll(tenantTasks);
        var results = tenantResults.SelectMany(r => r).ToList();

        logger.LogInformation("Found {Count} rightsizing recommendations", results.Count);
        return results;
    }

    private async Task<List<RightsizingRecommendation>> ProcessTenantAsync(
        string tenantId,
        List<TenantSubscription> subscriptions,
        Dictionary<string, TenantSubscription> subLookup)
    {
        try
        {
            var client = tenantClientManager.GetClientForTenant(tenantId);
            var subscriptionIds = subscriptions.Select(s => s.SubscriptionId).ToList();

            var query = """
                advisorresources
                | where type == "microsoft.advisor/recommendations"
                | where properties.category == "Cost"
                | where properties.shortDescription.solution contains "ize"
                   or properties.shortDescription.solution contains "hutdown"
                | project id, name, subscriptionId, resourceGroup, location,
                    impact = properties.impact,
                    title = properties.shortDescription.solution,
                    currentSku = properties.extendedProperties.currentSku,
                    targetSku = properties.extendedProperties.targetSku,
                    savingsAmount = properties.extendedProperties.savingsAmount,
                    savingsCurrency = properties.extendedProperties.savingsCurrency,
                    resourceType = properties.impactedField
                """;

            var content = new ResourceQueryContent(query)
            {
                Options = new ResourceQueryRequestOptions { ResultFormat = ResultFormat.ObjectArray }
            };

            foreach (var subId in subscriptionIds)
            {
                content.Subscriptions.Add(subId);
            }

            var tenant = client.GetTenants().First();
            var response = await tenant.GetResourcesAsync(content);
            var jsonElement = response.Value.Data.ToObjectFromJson<JsonElement>();

            if (jsonElement.ValueKind != JsonValueKind.Array)
                return [];

            var results = new List<RightsizingRecommendation>();

            foreach (var item in jsonElement.EnumerateArray())
            {
                try
                {
                    var resourceId = item.GetProperty("id").GetString() ?? "";
                    var resourceName = item.GetProperty("name").GetString() ?? "";
                    var subscriptionId = item.GetProperty("subscriptionId").GetString() ?? "";
                    var resourceGroup = item.GetProperty("resourceGroup").GetString() ?? "";
                    var location = item.GetProperty("location").GetString() ?? "";
                    var impact = item.TryGetProperty("impact", out var impactProp) ? impactProp.GetString() ?? "" : "";
                    var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
                    var resourceType = item.TryGetProperty("resourceType", out var typeProp) ? typeProp.GetString() ?? "" : "";

                    var currentSku = item.TryGetProperty("currentSku", out var currentSkuProp)
                        ? currentSkuProp.ValueKind == JsonValueKind.String ? currentSkuProp.GetString() : null
                        : null;

                    var targetSku = item.TryGetProperty("targetSku", out var targetSkuProp)
                        ? targetSkuProp.ValueKind == JsonValueKind.String ? targetSkuProp.GetString() : null
                        : null;

                    decimal? savingsAmount = null;
                    if (item.TryGetProperty("savingsAmount", out var savingsProp))
                    {
                        if (savingsProp.ValueKind == JsonValueKind.Number)
                            savingsAmount = savingsProp.GetDecimal();
                        else if (savingsProp.ValueKind == JsonValueKind.String &&
                                 decimal.TryParse(savingsProp.GetString(),
                                     System.Globalization.NumberStyles.Any,
                                     System.Globalization.CultureInfo.InvariantCulture,
                                     out var parsed))
                            savingsAmount = parsed;
                    }

                    var recommendedAction = string.IsNullOrEmpty(targetSku)
                        ? title
                        : $"Resize to {targetSku}";

                    var impactLevel = impact.ToLowerInvariant() switch
                    {
                        "high" => "High",
                        "medium" => "Medium",
                        "low" => "Low",
                        _ => impact
                    };

                    if (!subLookup.TryGetValue(subscriptionId, out var subscription))
                        continue;

                    results.Add(new RightsizingRecommendation
                    {
                        ResourceId = resourceId,
                        ResourceName = resourceName,
                        ResourceType = resourceType,
                        ResourceGroup = resourceGroup,
                        Location = location,
                        SubscriptionId = subscriptionId,
                        SubscriptionName = subscription.DisplayName,
                        TenantId = tenantId,
                        RecommendationTitle = title,
                        ImpactLevel = impactLevel,
                        Category = "Cost",
                        CurrentSku = currentSku,
                        RecommendedAction = recommendedAction,
                        PotentialSavingsMonthly = savingsAmount
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error parsing rightsizing recommendation result");
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing rightsizing recommendations for tenant {TenantId}", tenantId);
            return [];
        }
    }
}
