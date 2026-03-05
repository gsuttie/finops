using FinOps.Models;

namespace FinOps.Services;

public interface IUpsellService
{
    Task<IReadOnlyList<UpsellOpportunity>> GetOpportunitiesAsync(
        IEnumerable<TenantSubscription> subscriptions);
}
