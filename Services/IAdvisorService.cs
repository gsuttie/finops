using FinOps.Models;

namespace FinOps.Services;

public interface IAdvisorService
{
    Task<IReadOnlyList<AdvisorRecommendation>> GetRecommendationsAsync(
        IEnumerable<TenantSubscription> subscriptions);
}
