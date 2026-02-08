using FinOps.Models;

namespace FinOps.Services;

public interface IBudgetService
{
    Task<IReadOnlyList<BudgetInfo>> GetBudgetsAsync(TenantSubscription subscription);

    Task<IReadOnlyList<BudgetCreationResult>> CreateBudgetForSubscriptionsAsync(
        IEnumerable<TenantSubscription> subscriptions,
        BudgetFormModel form);

    Task DeleteBudgetAsync(BudgetInfo budget);
}
