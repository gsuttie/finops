namespace FinOps.Services;
using FinOps.Models;

public class ReservationService(
    ICostAnalysisService costAnalysisService) : IReservationService
{
    private static readonly Dictionary<string, (decimal OneYear, decimal ThreeYear)> ReservationLookup =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Virtual Machines"]              = (40m, 62m),
            ["SQL Database"]                  = (33m, 42m),
            ["Azure Cosmos DB"]               = (65m, 65m),
            ["App Service"]                   = (55m, 55m),
            ["Azure Cache for Redis"]         = (55m, 55m),
            ["Azure Databricks"]              = (55m, 55m),
            ["Managed Disks"]                 = (57m, 57m),
            ["Azure Database for MySQL"]      = (33m, 42m),
            ["Azure Database for PostgreSQL"] = (33m, 42m),
            ["Azure Synapse Analytics"]       = (65m, 65m),
            ["Azure Data Factory"]            = (36m, 49m),
            ["Azure Dedicated Host"]          = (40m, 62m),
            ["HDInsight"]                     = (40m, 62m),
            ["Azure VMware Solution"]         = (33m, 42m),
        };

    private const decimal MinMonthlySpend = 10m;

    public async Task<ReservationAnalysis> GetReservationCandidatesAsync(TenantSubscription subscription)
    {
        var now = DateTime.UtcNow;
        var startDate = now.AddDays(-30);

        var serviceTask = costAnalysisService.GetCostsByDimensionAsync(
            subscription.SubscriptionId, subscription.TenantId, startDate, now, "ServiceName", 50);
        var pricingTask = costAnalysisService.GetCostsByDimensionAsync(
            subscription.SubscriptionId, subscription.TenantId, startDate, now, "PricingModel", 10);

        await Task.WhenAll(serviceTask, pricingTask);
        await Task.Delay(500);

        var serviceItems = serviceTask.Result;
        var pricingItems = pricingTask.Result;

        var currency = serviceItems.FirstOrDefault()?.Currency ?? "USD";

        var totalPricingCost = pricingItems.Sum(p => p.Cost);
        var reservedCost = pricingItems
            .Where(p => p.Name.Contains("Reservation", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Cost);
        var coveragePercent = totalPricingCost > 0
            ? Math.Round(reservedCost / totalPricingCost * 100, 1) : 0m;

        var candidates = new List<ReservationCandidate>();
        foreach (var item in serviceItems)
        {
            if (!TryMatchLookup(item.Name, out var rates)) continue;
            if (item.Cost < MinMonthlySpend) continue;

            var monthly = item.Cost;
            var annual = monthly * 12;
            candidates.Add(new ReservationCandidate
            {
                ServiceName                      = item.Name,
                MonthlyCost                      = monthly,
                AnnualizedCost                   = annual,
                Currency                         = item.Currency,
                OneYearSavingPercent             = rates.OneYear,
                ThreeYearSavingPercent           = rates.ThreeYear,
                EstimatedMonthlySavingsOneYear   = Math.Round(monthly * rates.OneYear / 100, 2),
                EstimatedMonthlySavingsThreeYear = Math.Round(monthly * rates.ThreeYear / 100, 2),
                EstimatedAnnualSavingsOneYear    = Math.Round(annual * rates.OneYear / 100, 2),
                EstimatedAnnualSavingsThreeYear  = Math.Round(annual * rates.ThreeYear / 100, 2),
                Recommendation = monthly switch
                {
                    > 500m => "Buy 3-Year",
                    > 100m => "Buy 1-Year",
                    _      => "Evaluate"
                }
            });
        }

        candidates = candidates.OrderByDescending(c => c.EstimatedAnnualSavingsThreeYear).ToList();

        return new ReservationAnalysis
        {
            SubscriptionId                       = subscription.SubscriptionId,
            SubscriptionName                     = subscription.DisplayName,
            Currency                             = currency,
            TotalMonthlyPayGoCost                = serviceItems.Sum(i => i.Cost),
            CurrentReservationCoveragePercent    = coveragePercent,
            TotalPotentialAnnualSavingsOneYear   = candidates.Sum(c => c.EstimatedAnnualSavingsOneYear),
            TotalPotentialAnnualSavingsThreeYear = candidates.Sum(c => c.EstimatedAnnualSavingsThreeYear),
            Candidates                           = candidates,
            AllServiceItems                      = serviceItems
        };
    }

    // Exact match first, then falls back to contains-based matching in both directions.
    private static bool TryMatchLookup(string serviceName, out (decimal OneYear, decimal ThreeYear) rates)
    {
        if (ReservationLookup.TryGetValue(serviceName, out rates))
            return true;

        foreach (var (key, value) in ReservationLookup)
        {
            if (serviceName.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                key.Contains(serviceName, StringComparison.OrdinalIgnoreCase))
            {
                rates = value;
                return true;
            }
        }

        rates = default;
        return false;
    }
}
