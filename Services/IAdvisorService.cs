using FinOps.Models;

namespace FinOps.Services;

public interface IAdvisorService
{
    Task<IReadOnlyList<AdvisorRecommendation>> GetRecommendationsAsync(
        IEnumerable<TenantSubscription> subscriptions);

    Task<Dictionary<string, double>> GetAdvisorScoresAsync(
        IEnumerable<TenantSubscription> subscriptions);

    /// <summary>
    /// Gets the total potential cost savings from Advisor cost recommendations for given subscriptions
    /// </summary>
    Task<decimal> GetPotentialCostSavingsAsync(
        IEnumerable<TenantSubscription> subscriptions);
}
