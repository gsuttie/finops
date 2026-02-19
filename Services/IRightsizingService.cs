using FinOps.Models;

namespace FinOps.Services;

public interface IRightsizingService
{
    Task<IReadOnlyList<RightsizingRecommendation>> GetRecommendationsAsync(
        IEnumerable<TenantSubscription> subscriptions);
}
