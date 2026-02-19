using FinOps.Models;

namespace FinOps.Services;

public interface IMaturityService
{
    Task<IReadOnlyList<SubscriptionMaturityScore>> ScoreSubscriptionsAsync(
        IEnumerable<TenantSubscription> subscriptions,
        IProgress<int>? progress = null);
}
