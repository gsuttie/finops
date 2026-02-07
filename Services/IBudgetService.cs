using FinOps.Models;

namespace FinOps.Services;

public interface IBudgetService
{
    Task<IReadOnlyList<BudgetCreationResult>> CreateBudgetForSubscriptionsAsync(
        IEnumerable<TenantSubscription> subscriptions,
        BudgetFormModel form);
}
