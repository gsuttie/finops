using System.Text.Json;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using FinOps.Models;

namespace FinOps.Services;

public class HybridBenefitService(
    TenantClientManager tenantClientManager,
    ILogger<HybridBenefitService> logger) : IHybridBenefitService
{
    private const string Query = """
        resources
        | where type == 'microsoft.compute/virtualmachines'
        | where properties.storageProfile.osDisk.osType == 'Windows'
        | project
            id,
            name,
            resourceGroup,
            location,
            subscriptionId,
            vmSize = properties.hardwareProfile.vmSize,
            licenseType = properties.licenseType,
            powerState = properties.extended.instanceView.powerState.displayStatus
        """;

    public async Task<HybridBenefitAnalysis> GetHybridBenefitAnalysisAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subList = subscriptions.ToList();
        var subLookup = subList.ToDictionary(s => s.SubscriptionId, s => s);
        var byTenant = subList.GroupBy(s => s.TenantId);
        var allVms = new List<VmHybridBenefitInfo>();

        foreach (var tenantGroup in byTenant)
        {
            try
            {
                var client = tenantClientManager.GetClientForTenant(tenantGroup.Key);
                var content = new ResourceQueryContent(Query)
                {
                    Options = new ResourceQueryRequestOptions
                    {
                        ResultFormat = ResultFormat.ObjectArray
                    }
                };
                foreach (var sub in tenantGroup)
                    content.Subscriptions.Add(sub.SubscriptionId);

                var tenant = client.GetTenants().First();
                var response = await tenant.GetResourcesAsync(content);
                var json = response.Value.Data.ToObjectFromJson<JsonElement>();

                if (json.ValueKind != JsonValueKind.Array) continue;

                foreach (var item in json.EnumerateArray())
                {
                    try
                    {
                        var resourceId = item.GetProperty("id").GetString() ?? "";
                        var name = item.GetProperty("name").GetString() ?? "";
                        var resourceGroup = item.GetProperty("resourceGroup").GetString() ?? "";
                        var location = item.GetProperty("location").GetString() ?? "";
                        var subscriptionId = item.GetProperty("subscriptionId").GetString() ?? "";
                        var vmSize = item.TryGetProperty("vmSize", out var sizeProp)
                            ? sizeProp.GetString() ?? "" : "";
                        var licenseType = item.TryGetProperty("licenseType", out var ltProp)
                            ? ltProp.GetString() : null;
                        string? powerState = null;
                        if (item.TryGetProperty("powerState", out var psProp)
                            && psProp.ValueKind == JsonValueKind.String)
                            powerState = psProp.GetString();

                        var hybridEnabled = !string.IsNullOrEmpty(licenseType) &&
                            (licenseType.Equals("Windows_Server", StringComparison.OrdinalIgnoreCase) ||
                             licenseType.Equals("Windows_Client", StringComparison.OrdinalIgnoreCase));

                        subLookup.TryGetValue(subscriptionId, out var sub);

                        allVms.Add(new VmHybridBenefitInfo
                        {
                            ResourceId           = resourceId,
                            Name                 = name,
                            ResourceGroup        = resourceGroup,
                            Location             = location,
                            SubscriptionId       = subscriptionId,
                            SubscriptionName     = sub?.DisplayName ?? subscriptionId,
                            TenantId             = tenantGroup.Key,
                            VmSize               = vmSize,
                            PowerState           = powerState,
                            HybridBenefitEnabled = hybridEnabled
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to parse VM item");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to query hybrid benefit data for tenant {TenantId}", tenantGroup.Key);
            }
        }

        var total = allVms.Count;
        var enabled = allVms.Count(v => v.HybridBenefitEnabled);
        return new HybridBenefitAnalysis
        {
            TotalWindowsVms = total,
            EnabledCount    = enabled,
            CoveragePercent = total > 0 ? Math.Round((decimal)enabled / total * 100, 1) : 0m,
            Vms             = allVms.OrderBy(v => v.HybridBenefitEnabled)
                                    .ThenBy(v => v.Name)
                                    .ToList()
        };
    }
}
