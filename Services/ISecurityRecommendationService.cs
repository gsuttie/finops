using FinOps.Models;

namespace FinOps.Services;

public interface ISecurityRecommendationService
{
    Task<IReadOnlyList<SecurityRecommendation>> GetSecurityRecommendationsAsync(
        IEnumerable<TenantSubscription> subscriptions);
}
