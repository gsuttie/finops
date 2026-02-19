using System.Text.Json;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using FinOps.Models;

namespace FinOps.Services;

public class CarbonService(
    ICostAnalysisService costService,
    TenantClientManager tenantClientManager,
    ILogger<CarbonService> logger) : ICarbonService
{
    // gCO2e/kWh by Azure region key (lowercased, no spaces)
    private static readonly IReadOnlyDictionary<string, double> RegionIntensities =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["norwayeast"]       = 8,
            ["swedencentral"]    = 8,
            ["canadacentral"]    = 23,
            ["canadaeast"]       = 23,
            ["francecentral"]    = 56,
            ["francesouth"]      = 56,
            ["brazilsouth"]      = 86,
            ["brazilsoutheast"]  = 86,
            ["uksouth"]          = 231,
            ["ukwest"]           = 231,
            ["westeurope"]       = 283,
            ["northeurope"]      = 316,
            ["eastus"]           = 371,
            ["eastus2"]          = 371,
            ["westus"]           = 371,
            ["westus2"]          = 371,
            ["westus3"]          = 371,
            ["centralus"]        = 462,
            ["northcentralus"]   = 462,
            ["southcentralus"]   = 462,
            ["westcentralus"]    = 462,
            ["southeastasia"]    = 493,
            ["eastasia"]         = 493,
            ["japaneast"]        = 506,
            ["japanwest"]        = 506,
            ["koreacentral"]     = 450,
            ["koreasouth"]       = 450,
            ["australiaeast"]    = 760,
            ["australiasoutheast"] = 760,
            ["australiacentral"] = 760,
            ["indiacentral"]     = 724,
            ["indiasouth"]       = 724,
            ["indiawest"]        = 724,
            ["uaenorth"]         = 550,
            ["uaecentral"]       = 550,
            ["southafricanorth"] = 700,
            ["southafricawest"]  = 700,
        };

    // Watts per vCPU (perVcpu, base) by VM family letter
    private static readonly IReadOnlyDictionary<char, (double PerVCpu, double Base)> SkuFamilyWatts =
        new Dictionary<char, (double, double)>
        {
            ['B'] = (8,  10),
            ['D'] = (15, 20),
            ['E'] = (18, 20),
            ['F'] = (20, 20),
            ['N'] = (25, 150),
            ['H'] = (30, 50),
            ['L'] = (15, 25),
            ['M'] = (20, 50),
        };

    private static readonly IReadOnlyDictionary<string, string> RegionDisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["norwayeast"]         = "Norway East",
            ["swedencentral"]      = "Sweden Central",
            ["canadacentral"]      = "Canada Central",
            ["canadaeast"]         = "Canada East",
            ["francecentral"]      = "France Central",
            ["francesouth"]        = "France South",
            ["brazilsouth"]        = "Brazil South",
            ["brazilsoutheast"]    = "Brazil Southeast",
            ["uksouth"]            = "UK South",
            ["ukwest"]             = "UK West",
            ["westeurope"]         = "West Europe",
            ["northeurope"]        = "North Europe",
            ["eastus"]             = "East US",
            ["eastus2"]            = "East US 2",
            ["westus"]             = "West US",
            ["westus2"]            = "West US 2",
            ["westus3"]            = "West US 3",
            ["centralus"]          = "Central US",
            ["northcentralus"]     = "North Central US",
            ["southcentralus"]     = "South Central US",
            ["westcentralus"]      = "West Central US",
            ["southeastasia"]      = "Southeast Asia",
            ["eastasia"]           = "East Asia",
            ["japaneast"]          = "Japan East",
            ["japanwest"]          = "Japan West",
            ["koreacentral"]       = "Korea Central",
            ["koreasouth"]         = "Korea South",
            ["australiaeast"]      = "Australia East",
            ["australiasoutheast"] = "Australia Southeast",
            ["australiacentral"]   = "Australia Central",
            ["indiacentral"]       = "India Central",
            ["indiasouth"]         = "India South",
            ["indiawest"]          = "India West",
            ["uaenorth"]           = "UAE North",
            ["uaecentral"]         = "UAE Central",
            ["southafricanorth"]   = "South Africa North",
            ["southafricawest"]    = "South Africa West",
        };

    public IReadOnlyDictionary<string, double> GetRegionCarbonIntensities() => RegionIntensities;

    public async Task<IReadOnlyList<CarbonEstimate>> GetVmCarbonEstimatesAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subList = subscriptions.ToList();
        if (subList.Count == 0)
            return [];

        logger.LogInformation("Scanning {Count} subscriptions for VM carbon estimates", subList.Count);

        var subLookup = subList.ToDictionary(s => s.SubscriptionId, s => s);
        var tenantGroups = subList.GroupBy(s => s.TenantId);

        var tenantTasks = tenantGroups.Select(group =>
            ProcessTenantVmsAsync(group.Key, group.ToList(), subLookup));

        var tenantResults = await Task.WhenAll(tenantTasks);
        var results = tenantResults.SelectMany(r => r).ToList();

        logger.LogInformation("Found {Count} VMs for carbon estimation", results.Count);
        return results;
    }

    public async Task<IReadOnlyList<RegionCarbonSummary>> GetRegionCarbonSummariesAsync(
        IEnumerable<TenantSubscription> subscriptions,
        IReadOnlyList<CarbonEstimate> vmEstimates,
        DateTime from,
        DateTime to)
    {
        var subList = subscriptions.ToList();
        if (subList.Count == 0)
            return [];

        // Group VM carbon by normalised region key
        var carbonByRegion = vmEstimates
            .GroupBy(v => NormaliseLocation(v.Location))
            .ToDictionary(g => g.Key, g => new
            {
                MonthlyKgCo2e = g.Sum(v => v.MonthlyKgCo2e),
                Intensity     = g.First().CarbonIntensityGCo2PerKwh,
                Count         = g.Count(),
                Location      = g.Key
            });

        // Fetch cost by ResourceLocation per subscription with 300ms delay
        var costByRegion = new Dictionary<string, (decimal Cost, string Currency)>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < subList.Count; i++)
        {
            var sub = subList[i];
            try
            {
                var breakdown = await costService.GetCostsByDimensionAsync(
                    sub.SubscriptionId, sub.TenantId, from, to, "ResourceLocation", 50);

                foreach (var item in breakdown)
                {
                    var key = NormaliseLocation(item.Name);
                    if (costByRegion.TryGetValue(key, out var existing))
                        costByRegion[key] = (existing.Cost + item.Cost, item.Currency ?? "USD");
                    else
                        costByRegion[key] = (item.Cost, item.Currency ?? "USD");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get cost breakdown for subscription {SubId}", sub.SubscriptionId);
            }

            if (i < subList.Count - 1)
                await Task.Delay(300);
        }

        // Join carbon + cost by region
        var allRegions = carbonByRegion.Keys.Union(costByRegion.Keys, StringComparer.OrdinalIgnoreCase).ToList();

        var summaries = new List<RegionCarbonSummary>();

        foreach (var regionKey in allRegions)
        {
            carbonByRegion.TryGetValue(regionKey, out var carbon);
            costByRegion.TryGetValue(regionKey, out var cost);

            var intensity = carbon?.Intensity
                ?? (RegionIntensities.TryGetValue(regionKey, out var i2) ? i2 : 500);

            summaries.Add(new RegionCarbonSummary
            {
                Location               = regionKey,
                RegionDisplayName      = GetDisplayName(regionKey),
                CarbonIntensityGCo2PerKwh = intensity,
                MonthlyCost            = cost.Cost,
                Currency               = cost.Currency ?? "USD",
                MonthlyKgCo2e          = carbon?.MonthlyKgCo2e ?? 0,
                ResourceCount          = carbon?.Count ?? 0
            });
        }

        return summaries.OrderBy(s => s.CarbonIntensityGCo2PerKwh).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<List<CarbonEstimate>> ProcessTenantVmsAsync(
        string tenantId,
        List<TenantSubscription> subscriptions,
        Dictionary<string, TenantSubscription> subLookup)
    {
        try
        {
            var client = tenantClientManager.GetClientForTenant(tenantId);
            var subscriptionIds = subscriptions.Select(s => s.SubscriptionId).ToList();
            var subIdsCsv = string.Join("','", subscriptionIds);

            var query = $"""
                resources
                | where type =~ 'microsoft.compute/virtualmachines'
                | where subscriptionId in ('{subIdsCsv}')
                | project name, resourceGroup, location, subscriptionId,
                          skuName = tostring(properties.hardwareProfile.vmSize)
                | order by location asc
                """;

            var content = new ResourceQueryContent(query)
            {
                Options = new ResourceQueryRequestOptions { ResultFormat = ResultFormat.ObjectArray }
            };

            foreach (var subId in subscriptionIds)
                content.Subscriptions.Add(subId);

            var tenant = client.GetTenants().First();
            var response = await tenant.GetResourcesAsync(content);
            var jsonElement = response.Value.Data.ToObjectFromJson<JsonElement>();

            if (jsonElement.ValueKind != JsonValueKind.Array)
                return [];

            var results = new List<CarbonEstimate>();

            foreach (var item in jsonElement.EnumerateArray())
            {
                try
                {
                    var name           = item.GetProperty("name").GetString() ?? "";
                    var resourceGroup  = item.GetProperty("resourceGroup").GetString() ?? "";
                    var location       = item.GetProperty("location").GetString() ?? "";
                    var subscriptionId = item.GetProperty("subscriptionId").GetString() ?? "";
                    var skuName        = item.TryGetProperty("skuName", out var skuProp)
                                         && skuProp.ValueKind == JsonValueKind.String
                                         ? skuProp.GetString() ?? ""
                                         : "";

                    if (!subLookup.TryGetValue(subscriptionId, out var sub))
                        continue;

                    var locKey    = NormaliseLocation(location);
                    var intensity = RegionIntensities.TryGetValue(locKey, out var gi) ? gi : 500;
                    var (watts, _, _) = ParseEstimatedWatts(skuName);
                    var monthlyKg = CalculateMonthlyKgCo2e(watts, intensity);

                    results.Add(new CarbonEstimate
                    {
                        ResourceName              = name,
                        ResourceType              = "microsoft.compute/virtualmachines",
                        ResourceGroup             = resourceGroup,
                        Location                  = locKey,
                        SubscriptionId            = subscriptionId,
                        SubscriptionName          = sub.DisplayName,
                        SkuName                   = skuName,
                        CarbonIntensityGCo2PerKwh = intensity,
                        EstimatedWatts            = watts,
                        MonthlyKgCo2e             = monthlyKg,
                        RegionDisplayName         = GetDisplayName(locKey)
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error parsing VM carbon estimate result");
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing VMs for tenant {TenantId}", tenantId);
            return [];
        }
    }

    /// <summary>
    /// Parse estimated watts from an Azure VM SKU name.
    /// E.g. "Standard_D4s_v3" → family=D, vcpus=4, watts=80
    /// </summary>
    internal static (double Watts, char Family, int VCpus) ParseEstimatedWatts(string skuName)
    {
        if (string.IsNullOrWhiteSpace(skuName))
            return (35, 'D', 2);

        var parts = skuName.Split('_', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            // Skip known prefix/suffix tokens
            var upper = part.ToUpperInvariant();
            if (upper is "STANDARD" or "BASIC" or "PREMIUM" or "ULTRASSD" or "EPHEMERAL")
                continue;
            if (upper.StartsWith('V') && upper.Length <= 3 && upper[1..].All(char.IsDigit))
                continue; // version like "v3", "v5"

            // Find a segment that starts with letters and contains digits (e.g. "D4s", "NC6", "E16as")
            var i = 0;
            while (i < part.Length && char.IsLetter(part[i])) i++;

            if (i == 0 || i >= part.Length || !char.IsDigit(part[i]))
                continue;

            var familyStr = part[..i].ToUpperInvariant();
            var family    = familyStr[^1]; // last char of family string (e.g. "NC" → 'C', "D" → 'D')
            // For compound families like "NC", "NV", "ND" we use 'N' for the lookup
            var lookupFamily = SkuFamilyWatts.ContainsKey(family) ? family :
                               (familyStr.Length > 0 && SkuFamilyWatts.ContainsKey(familyStr[0]) ? familyStr[0] : 'D');

            var j = i;
            while (j < part.Length && char.IsDigit(part[j])) j++;
            if (!int.TryParse(part[i..j], out var vcpus) || vcpus <= 0)
                continue;

            if (!SkuFamilyWatts.TryGetValue(lookupFamily, out var fw))
                fw = (15, 20); // fallback defaults

            var watts = fw.PerVCpu * vcpus + fw.Base;
            return (watts, lookupFamily, vcpus);
        }

        return (35, 'D', 2);
    }

    private static double CalculateMonthlyKgCo2e(double watts, double intensityGCo2PerKwh)
        => watts / 1000.0 * 730 * intensityGCo2PerKwh / 1000.0;

    private static string NormaliseLocation(string location)
        => location.ToLowerInvariant().Replace(" ", "");

    private static string GetDisplayName(string locationKey)
        => RegionDisplayNames.TryGetValue(locationKey, out var name) ? name : locationKey;
}
