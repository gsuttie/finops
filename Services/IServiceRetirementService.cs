using FinOps.Models;

namespace FinOps.Services;

public interface IServiceRetirementService
{
    Task<IReadOnlyList<ServiceRetirement>> GetServiceRetirementsAsync(
        IEnumerable<TenantSubscription> subscriptions);
}
