using System.Text.Json;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Azure.ResourceManager.Resources;
using FinOps.Models;
using Microsoft.Extensions.Logging;

namespace FinOps.Services;

public class PrivateEndpointService(
    TenantClientManager tenantClientManager,
    ILogger<PrivateEndpointService> logger) : IPrivateEndpointService
{
    // Resource types that support private endpoints
    private static readonly Dictionary<string, (string Query, string DisplayName, string RecommendationReason)> ResourceTypeQueries = new()
    {
        ["Storage Account"] = (
            @"resources 
            | where type == 'microsoft.storage/storageaccounts'
            | extend hasPrivateEndpoint = isnotnull(properties.privateEndpointConnections) and array_length(properties.privateEndpointConnections) > 0
            | extend publicNetworkAccess = tostring(properties.publicNetworkAccess)
            | extend allowBlobPublicAccess = tostring(properties.allowBlobPublicAccess)
            | project id, name, type, resourceGroup, location, subscriptionId, hasPrivateEndpoint, publicNetworkAccess, allowBlobPublicAccess",
            "Storage Account",
            "Storage account is publicly accessible and could benefit from private endpoints for secure access"),

        ["Key Vault"] = (
            @"resources 
            | where type == 'microsoft.keyvault/vaults'
            | extend hasPrivateEndpoint = isnotnull(properties.privateEndpointConnections) and array_length(properties.privateEndpointConnections) > 0
            | extend publicNetworkAccess = tostring(properties.publicNetworkAccess)
            | extend networkAcls = tostring(properties.networkAcls.defaultAction)
            | project id, name, type, resourceGroup, location, subscriptionId, hasPrivateEndpoint, publicNetworkAccess, networkAcls",
            "Key Vault",
            "Key Vault is accessible from public networks and should use private endpoints for enhanced security"),

        ["SQL Database"] = (
            @"resources 
            | where type == 'microsoft.sql/servers'
            | extend hasPrivateEndpoint = isnotnull(properties.privateEndpointConnections) and array_length(properties.privateEndpointConnections) > 0
            | extend publicNetworkAccess = tostring(properties.publicNetworkAccess)
            | project id, name, type, resourceGroup, location, subscriptionId, hasPrivateEndpoint, publicNetworkAccess",
            "SQL Server",
            "SQL Server allows public network access and should use private endpoints to restrict access"),

        ["Cosmos DB"] = (
            @"resources 
            | where type == 'microsoft.documentdb/databaseaccounts'
            | extend hasPrivateEndpoint = isnotnull(properties.privateEndpointConnections) and array_length(properties.privateEndpointConnections) > 0
            | extend publicNetworkAccess = tostring(properties.publicNetworkAccess)
            | project id, name, type, resourceGroup, location, subscriptionId, hasPrivateEndpoint, publicNetworkAccess",
            "Cosmos DB Account",
            "Cosmos DB account is publicly accessible and should use private endpoints for secure data access"),

        ["Container Registry"] = (
            @"resources 
            | where type == 'microsoft.containerregistry/registries'
            | extend hasPrivateEndpoint = isnotnull(properties.privateEndpointConnections) and array_length(properties.privateEndpointConnections) > 0
            | extend publicNetworkAccess = tostring(properties.publicNetworkAccess)
            | extend networkRuleSet = tostring(properties.networkRuleSet.defaultAction)
            | project id, name, type, resourceGroup, location, subscriptionId, hasPrivateEndpoint, publicNetworkAccess, networkRuleSet",
            "Container Registry",
            "Container Registry allows public access and should use private endpoints to secure container images"),

        ["PostgreSQL Server"] = (
            @"resources 
            | where type == 'microsoft.dbforpostgresql/servers' or type == 'microsoft.dbforpostgresql/flexibleservers'
            | extend hasPrivateEndpoint = isnotnull(properties.privateEndpointConnections) and array_length(properties.privateEndpointConnections) > 0
            | extend publicNetworkAccess = tostring(properties.publicNetworkAccess)
            | project id, name, type, resourceGroup, location, subscriptionId, hasPrivateEndpoint, publicNetworkAccess",
            "PostgreSQL Server",
            "PostgreSQL server is publicly accessible and should use private endpoints for secure database access"),

        ["MySQL Server"] = (
            @"resources 
            | where type == 'microsoft.dbformysql/servers' or type == 'microsoft.dbformysql/flexibleservers'
            | extend hasPrivateEndpoint = isnotnull(properties.privateEndpointConnections) and array_length(properties.privateEndpointConnections) > 0
            | extend publicNetworkAccess = tostring(properties.publicNetworkAccess)
            | project id, name, type, resourceGroup, location, subscriptionId, hasPrivateEndpoint, publicNetworkAccess",
            "MySQL Server",
            "MySQL server is publicly accessible and should use private endpoints for secure database access"),

        ["App Service"] = (
            @"resources 
            | where type == 'microsoft.web/sites' and kind !contains 'functionapp'
            | extend hasPrivateEndpoint = isnotnull(properties.privateEndpointConnections) and array_length(properties.privateEndpointConnections) > 0
            | extend publicNetworkAccess = tostring(properties.publicNetworkAccess)
            | project id, name, type, resourceGroup, location, subscriptionId, hasPrivateEndpoint, publicNetworkAccess",
            "App Service",
            "App Service is publicly accessible and could use private endpoints for secure access from VNet")
    };

    public async Task<IReadOnlyList<PrivateEndpointRecommendation>> GetPrivateEndpointRecommendationsAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subList = subscriptions.ToList();
        if (subList.Count == 0)
            return [];

        logger.LogInformation("Scanning {Count} subscriptions for private endpoint recommendations", subList.Count);

        var results = new List<PrivateEndpointRecommendation>();
        var subLookup = subList.ToDictionary(s => s.SubscriptionId, s => s);

        // Group subscriptions by tenant
        var tenantGroups = subList.GroupBy(s => s.TenantId);

        var tenantTasks = tenantGroups.Select(group => ProcessTenantAsync(group.Key, group.ToList(), subLookup));
        var tenantResults = await Task.WhenAll(tenantTasks);

        foreach (var batch in tenantResults)
        {
            results.AddRange(batch);
        }

        logger.LogInformation("Found {Count} private endpoint recommendations", results.Count);
        return results;
    }

    public async Task<int> GetRecommendationCountAsync(IEnumerable<TenantSubscription> subscriptions)
    {
        var recommendations = await GetPrivateEndpointRecommendationsAsync(subscriptions);
        return recommendations.Count;
    }

    private async Task<List<PrivateEndpointRecommendation>> ProcessTenantAsync(
        string tenantId,
        List<TenantSubscription> subscriptions,
        Dictionary<string, TenantSubscription> subLookup)
    {
        try
        {
            var client = tenantClientManager.GetClientForTenant(tenantId);
            var subscriptionIds = subscriptions.Select(s => s.SubscriptionId).ToList();

            // Run all Resource Graph queries in parallel for this tenant
            var queryTasks = ResourceTypeQueries.Select(kvp =>
                RunResourceGraphQueryAsync(
                    client.GetTenants().First(),
                    subscriptionIds,
                    kvp.Value.Query,
                    kvp.Value.DisplayName,
                    kvp.Value.RecommendationReason,
                    subLookup,
                    tenantId));

            var allResults = await Task.WhenAll(queryTasks);
            return allResults.SelectMany(r => r).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing tenant {TenantId}", tenantId);
            return [];
        }
    }

    private async Task<List<PrivateEndpointRecommendation>> RunResourceGraphQueryAsync(
        TenantResource tenant,
        List<string> subscriptionIds,
        string query,
        string displayName,
        string recommendationReason,
        Dictionary<string, TenantSubscription> subLookup,
        string tenantId)
    {
        var results = new List<PrivateEndpointRecommendation>();

        try
        {
            var content = new ResourceQueryContent(query)
            {
                Options = new ResourceQueryRequestOptions { ResultFormat = ResultFormat.ObjectArray }
            };

            foreach (var subId in subscriptionIds)
            {
                content.Subscriptions.Add(subId);
            }

            var response = await tenant.GetResourcesAsync(content);
            var jsonElement = response.Value.Data.ToObjectFromJson<JsonElement>();

            if (jsonElement.ValueKind != JsonValueKind.Array)
                return results;

            foreach (var item in jsonElement.EnumerateArray())
            {
                try
                {
                    var resourceId = item.GetProperty("id").GetString() ?? "";
                    var resourceName = item.GetProperty("name").GetString() ?? "";
                    var resourceType = item.GetProperty("type").GetString() ?? "";
                    var resourceGroup = item.GetProperty("resourceGroup").GetString() ?? "";
                    var location = item.GetProperty("location").GetString() ?? "";
                    var subscriptionId = item.GetProperty("subscriptionId").GetString() ?? "";

                    // Check if resource has private endpoint
                    var hasPrivateEndpoint = item.TryGetProperty("hasPrivateEndpoint", out var hasPrivateEndpointProp) &&
                                           hasPrivateEndpointProp.ValueKind == JsonValueKind.True;

                    // Only recommend if no private endpoint exists
                    if (!hasPrivateEndpoint)
                    {
                        var publicNetworkAccess = item.TryGetProperty("publicNetworkAccess", out var publicProp)
                            ? publicProp.GetString() ?? "Unknown"
                            : "Unknown";

                        var networkAcls = item.TryGetProperty("networkAcls", out var aclsProp)
                            ? aclsProp.GetString()
                            : item.TryGetProperty("networkRuleSet", out var ruleSetProp)
                                ? ruleSetProp.GetString()
                                : null;

                        var currentState = publicNetworkAccess.Equals("Disabled", StringComparison.OrdinalIgnoreCase)
                            ? "Public access disabled (no private endpoint configured)"
                            : "Public access enabled";

                        if (!subLookup.TryGetValue(subscriptionId, out var subscription))
                            continue;

                        results.Add(new PrivateEndpointRecommendation
                        {
                            ResourceId = resourceId,
                            ResourceName = resourceName,
                            ResourceType = displayName,
                            ResourceGroup = resourceGroup,
                            Location = location,
                            SubscriptionId = subscriptionId,
                            SubscriptionName = subscription.DisplayName,
                            TenantId = tenantId,
                            CurrentState = currentState,
                            RecommendationReason = recommendationReason,
                            HasPublicAccess = !publicNetworkAccess.Equals("Disabled", StringComparison.OrdinalIgnoreCase),
                            NetworkAcls = networkAcls,
                            SupportedPrivateEndpointTypes = [resourceType]
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error parsing resource result for {DisplayName}", displayName);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing Resource Graph query for {DisplayName}", displayName);
        }

        return results;
    }
}
