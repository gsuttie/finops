using FinOps.Models;

namespace FinOps.Services;

public interface ICarbonService
{
    Task<IReadOnlyList<CarbonEstimate>> GetVmCarbonEstimatesAsync(IEnumerable<TenantSubscription> subscriptions);

    Task<IReadOnlyList<RegionCarbonSummary>> GetRegionCarbonSummariesAsync(
        IEnumerable<TenantSubscription> subscriptions,
        IReadOnlyList<CarbonEstimate> vmEstimates,
        DateTime from,
        DateTime to);

    IReadOnlyDictionary<string, double> GetRegionCarbonIntensities();
}
