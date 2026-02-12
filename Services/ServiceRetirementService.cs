using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using FinOps.Models;
using System.Text.Json;

namespace FinOps.Services;

public class ServiceRetirementService(TenantClientManager tenantClientManager) : IServiceRetirementService
{
    private readonly TenantClientManager _tenantClientManager = tenantClientManager;

    public async Task<IReadOnlyList<ServiceRetirement>> GetServiceRetirementsAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subscriptionsList = subscriptions.ToList();
        if (subscriptionsList.Count == 0)
        {
            return Array.Empty<ServiceRetirement>();
        }

        // Create lookup dictionary for subscription info
        var subscriptionLookup = subscriptionsList.ToDictionary(
            s => s.SubscriptionId,
            s => s
        );

        // Group subscriptions by tenant for parallel processing
        var groupedByTenant = subscriptionsList
            .GroupBy(s => s.TenantId)
            .ToList();

        var allRetirements = new List<ServiceRetirement>();

        // Process each tenant in parallel
        var tenantTasks = groupedByTenant.Select(async tenantGroup =>
        {
            try
            {
                return await ProcessTenantAsync(tenantGroup.Key, tenantGroup.ToList(), subscriptionLookup);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing tenant {tenantGroup.Key}: {ex.Message}");
                return new List<ServiceRetirement>();
            }
        });

        var results = await Task.WhenAll(tenantTasks);
        foreach (var result in results)
        {
            allRetirements.AddRange(result);
        }

        return allRetirements;
    }

    private async Task<List<ServiceRetirement>> ProcessTenantAsync(
        string tenantId,
        List<TenantSubscription> subscriptions,
        Dictionary<string, TenantSubscription> subscriptionLookup)
    {
        var client = _tenantClientManager.GetClientForTenant(tenantId);
        var tenant = client.GetTenants().First();
        var subscriptionIds = subscriptions.Select(s => s.SubscriptionId).ToList();

        var query = @"
advisorresources
| where type == 'microsoft.advisor/recommendations'
| where properties.category == 'HighAvailability'
| where properties.extendedProperties.recommendationSubCategory == 'ServiceUpgradeAndRetirement'
| extend retirementFeatureName = tostring(properties.extendedProperties.retirementFeatureName)
| extend retirementDate = tostring(properties.extendedProperties.retirementDate)
| extend resourceId = tostring(properties.resourceMetadata.resourceId)
| extend description = tostring(properties.shortDescription.problem)
| where retirementFeatureName != ''
| extend resourceType = tostring(strcat(split(resourceId, '/')[-3], '/', split(resourceId, '/')[-2]))
| extend resourceGroup = tostring(split(resourceId, '/')[4])
| extend location = tostring(properties.resourceMetadata.location)
| project retirementFeatureName, retirementDate, resourceId, description, resourceType, resourceGroup, location, subscriptionId";

        var content = new ResourceQueryContent(query);
        foreach (var subId in subscriptionIds)
        {
            content.Subscriptions.Add(subId);
        }

        var response = await tenant.GetResourcesAsync(content);
        var retirements = new List<ServiceRetirement>();

        // Parse results and group by (RetiringFeature + RetirementDate + SubscriptionId)
        var groupedResults = new Dictionary<string, (string feature, string date, string subId, string desc, string resourceType, List<RetirementResource> resources)>();

        var dataElement = response.Value.Data.ToObjectFromJson<JsonElement>();
        if (dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in dataElement.EnumerateArray())
            {
                try
                {
                    var retirementFeatureName = row.TryGetProperty("retirementFeatureName", out var fnProp)
                        ? fnProp.GetString() ?? string.Empty
                        : string.Empty;
                    var retirementDate = row.TryGetProperty("retirementDate", out var rdProp)
                        ? rdProp.GetString() ?? string.Empty
                        : string.Empty;
                    var resourceId = row.TryGetProperty("resourceId", out var riProp)
                        ? riProp.GetString() ?? string.Empty
                        : string.Empty;
                    var description = row.TryGetProperty("description", out var descProp)
                        ? descProp.GetString() ?? string.Empty
                        : string.Empty;
                    var resourceType = row.TryGetProperty("resourceType", out var rtProp)
                        ? rtProp.GetString() ?? string.Empty
                        : string.Empty;
                    var resourceGroup = row.TryGetProperty("resourceGroup", out var rgProp)
                        ? rgProp.GetString() ?? string.Empty
                        : string.Empty;
                    var location = row.TryGetProperty("location", out var locProp)
                        ? locProp.GetString() ?? string.Empty
                        : string.Empty;
                    var subId = row.TryGetProperty("subscriptionId", out var sidProp)
                        ? sidProp.GetString() ?? string.Empty
                        : string.Empty;

                    if (string.IsNullOrEmpty(retirementFeatureName) || string.IsNullOrEmpty(subId) || string.IsNullOrEmpty(resourceId))
                    {
                        continue;
                    }

                    var groupKey = $"{retirementFeatureName}|{retirementDate}|{subId}";

                    if (!groupedResults.ContainsKey(groupKey))
                    {
                        groupedResults[groupKey] = (retirementFeatureName, retirementDate, subId, description, resourceType, new List<RetirementResource>());
                    }

                    // Get subscription name for the resource
                    var subName = subscriptionLookup.TryGetValue(subId, out var sub) ? sub.DisplayName : subId;

                    groupedResults[groupKey].resources.Add(new RetirementResource
                    {
                        ResourceId = resourceId,
                        ResourceType = resourceType,
                        ResourceGroup = resourceGroup,
                        Location = location,
                        SubscriptionName = subName,
                        SubscriptionId = subId,
                        RetiringFeature = retirementFeatureName,
                        RetirementDate = retirementDate
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing retirement row: {ex.Message}");
                }
            }
        }

        // Convert grouped results to ServiceRetirement objects
        foreach (var kvp in groupedResults)
        {
            var (feature, date, subId, desc, resourceType, resources) = kvp.Value;

            if (!subscriptionLookup.TryGetValue(subId, out var subscription))
            {
                continue;
            }

            var serviceName = ExtractServiceName(resourceType);

            retirements.Add(new ServiceRetirement
            {
                ServiceName = serviceName,
                RetiringFeature = feature,
                RetirementDate = date,
                ResourceCount = resources.Count,
                Description = desc,
                SubscriptionName = subscription.DisplayName,
                SubscriptionId = subId,
                TenantId = tenantId,
                Resources = resources
            });
        }

        return retirements;
    }

    private static string ExtractServiceName(string resourceType)
    {
        if (string.IsNullOrEmpty(resourceType))
        {
            return "Unknown";
        }

        try
        {
            // Resource type format: "Microsoft.Compute/virtualMachines"
            // Extract the service name between "Microsoft." and "/"
            var parts = resourceType.Split('/');
            if (parts.Length > 0)
            {
                var providerPart = parts[0];
                if (providerPart.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
                {
                    return providerPart.Substring("Microsoft.".Length);
                }
                return providerPart;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting service name from '{resourceType}': {ex.Message}");
        }

        return "Unknown";
    }
}
