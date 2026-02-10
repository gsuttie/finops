using System.Text.Json;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Azure.ResourceManager.Resources;
using FinOps.Models;

namespace FinOps.Services;

public class OrphanedResourceService(TenantClientManager tenantClientManager) : IOrphanedResourceService
{
    private static readonly Dictionary<string, (string Query, string Category, string ResourceType)> ResourceGraphQueries = new()
    {
        ["Unattached Disk"] = (
            "resources | where type == 'microsoft.compute/disks' | where isnull(properties.managedBy) | project id, name, type, resourceGroup, location, subscriptionId",
            "Unattached Disk",
            "microsoft.compute/disks"),
        ["Unattached NIC"] = (
            "resources | where type == 'microsoft.network/networkinterfaces' | where isnull(properties.virtualMachine) | project id, name, type, resourceGroup, location, subscriptionId",
            "Unattached NIC",
            "microsoft.network/networkinterfaces"),
        ["Unassociated Public IP"] = (
            "resources | where type == 'microsoft.network/publicipaddresses' | where isnull(properties.ipConfiguration) | project id, name, type, resourceGroup, location, subscriptionId",
            "Unassociated Public IP",
            "microsoft.network/publicipaddresses"),
        ["Unassociated NSG"] = (
            "resources | where type == 'microsoft.network/networksecuritygroups' | where (isnull(properties.networkInterfaces) or array_length(properties.networkInterfaces) == 0) | where (isnull(properties.subnets) or array_length(properties.subnets) == 0) | project id, name, type, resourceGroup, location, subscriptionId",
            "Unassociated NSG",
            "microsoft.network/networksecuritygroups"),
        ["Empty Availability Set"] = (
            "resources | where type == 'microsoft.compute/availabilitysets' | where (isnull(properties.virtualMachines) or array_length(properties.virtualMachines) == 0) | project id, name, type, resourceGroup, location, subscriptionId",
            "Empty Availability Set",
            "microsoft.compute/availabilitysets"),
    };

    public async Task<IReadOnlyList<OrphanedResourceInfo>> ScanForOrphanedResourcesAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subList = subscriptions.ToList();
        if (subList.Count == 0)
            return [];

        var results = new List<OrphanedResourceInfo>();
        var subLookup = subList.ToDictionary(s => s.SubscriptionId, s => s);

        // Group subscriptions by tenant
        var tenantGroups = subList.GroupBy(s => s.TenantId);

        var tenantTasks = tenantGroups.Select(group => ProcessTenantAsync(group.Key, group.ToList(), subLookup));
        var tenantResults = await Task.WhenAll(tenantTasks);

        foreach (var batch in tenantResults)
        {
            results.AddRange(batch);
        }

        return results;
    }

    private async Task<List<OrphanedResourceInfo>> ProcessTenantAsync(
        string tenantId,
        List<TenantSubscription> subscriptions,
        Dictionary<string, TenantSubscription> subLookup)
    {
        var client = tenantClientManager.GetClientForTenant(tenantId);
        var subscriptionIds = subscriptions.Select(s => s.SubscriptionId).ToList();

        // Run all Resource Graph queries in parallel for this tenant
        var queryTasks = ResourceGraphQueries.Select(kvp =>
            RunResourceGraphQueryAsync(client.GetTenants().First(), subscriptionIds, kvp.Value.Query, kvp.Value.Category, kvp.Value.ResourceType, subLookup));

        var emptyRgTask = FindEmptyResourceGroupsAsync(tenantId, subscriptions, subLookup);

        var allTasks = queryTasks.Append(emptyRgTask);
        var batchResults = await Task.WhenAll(allTasks);

        return batchResults.SelectMany(r => r).ToList();
    }

    private static async Task<List<OrphanedResourceInfo>> RunResourceGraphQueryAsync(
        TenantResource tenant,
        List<string> subscriptionIds,
        string query,
        string category,
        string resourceType,
        Dictionary<string, TenantSubscription> subLookup)
    {
        var results = new List<OrphanedResourceInfo>();

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

        if (jsonElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in jsonElement.EnumerateArray())
            {
                var subscriptionId = row.GetProperty("subscriptionId").GetString() ?? "";
                var sub = subLookup.GetValueOrDefault(subscriptionId);

                results.Add(new OrphanedResourceInfo
                {
                    ResourceId = row.GetProperty("id").GetString() ?? "",
                    Name = row.GetProperty("name").GetString() ?? "",
                    ResourceType = resourceType,
                    Category = category,
                    ResourceGroup = row.GetProperty("resourceGroup").GetString() ?? "",
                    Location = row.GetProperty("location").GetString() ?? "",
                    SubscriptionName = sub?.DisplayName ?? subscriptionId,
                    SubscriptionId = subscriptionId,
                    TenantId = sub?.TenantId ?? ""
                });
            }
        }

        return results;
    }

    private async Task<List<OrphanedResourceInfo>> FindEmptyResourceGroupsAsync(
        string tenantId,
        List<TenantSubscription> subscriptions,
        Dictionary<string, TenantSubscription> subLookup)
    {
        var results = new List<OrphanedResourceInfo>();
        var client = tenantClientManager.GetClientForTenant(tenantId);

        foreach (var sub in subscriptions)
        {
            var subscriptionResource = client.GetSubscriptionResource(
                SubscriptionResource.CreateResourceIdentifier(sub.SubscriptionId));

            await foreach (var rg in subscriptionResource.GetResourceGroups().GetAllAsync())
            {
                var hasResources = false;
                await foreach (var _ in rg.GetGenericResourcesAsync())
                {
                    hasResources = true;
                    break;
                }

                if (!hasResources)
                {
                    results.Add(new OrphanedResourceInfo
                    {
                        ResourceId = rg.Id.ToString(),
                        Name = rg.Data.Name,
                        ResourceType = "microsoft.resources/resourcegroups",
                        Category = "Empty Resource Group",
                        ResourceGroup = rg.Data.Name,
                        Location = rg.Data.Location.DisplayName ?? rg.Data.Location.Name,
                        SubscriptionName = sub.DisplayName,
                        SubscriptionId = sub.SubscriptionId,
                        TenantId = sub.TenantId
                    });
                }
            }
        }

        return results;
    }
}
