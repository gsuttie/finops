using FinOps.Models;

namespace FinOps.Services;

public interface IHybridBenefitService
{
    Task<HybridBenefitAnalysis> GetHybridBenefitAnalysisAsync(IEnumerable<TenantSubscription> subscriptions);
}
