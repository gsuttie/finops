using FinOps.Models;

namespace FinOps.Services;

public interface IAzureSubscriptionService
{
    Task<IReadOnlyList<TenantSubscription>> GetSubscriptionsAsync();
}
