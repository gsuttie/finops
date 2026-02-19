using FinOps.Models;

namespace FinOps.Services;

public interface IPrivateEndpointService
{
    /// <summary>
    /// Gets private endpoint recommendations for resources in the specified subscriptions
    /// </summary>
    Task<IReadOnlyList<PrivateEndpointRecommendation>> GetPrivateEndpointRecommendationsAsync(
        IEnumerable<TenantSubscription> subscriptions);

    /// <summary>
    /// Gets the count of resources that could benefit from private endpoints
    /// </summary>
    Task<int> GetRecommendationCountAsync(
        IEnumerable<TenantSubscription> subscriptions);
}
