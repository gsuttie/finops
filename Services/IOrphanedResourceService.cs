using FinOps.Models;

namespace FinOps.Services;

public interface IOrphanedResourceService
{
    Task<IReadOnlyList<OrphanedResourceInfo>> ScanForOrphanedResourcesAsync(
        IEnumerable<TenantSubscription> subscriptions);
}
