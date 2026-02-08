using FinOps.Models;

namespace FinOps.Services;

public interface IBudgetService
{
    Task<IReadOnlyList<BudgetInfo>> GetBudgetsAsync(TenantSubscription subscription);

    Task<IReadOnlyList<BudgetCreationResult>> CreateBudgetForSubscriptionsAsync(
        IEnumerable<TenantSubscription> subscriptions,
        BudgetFormModel form);

    Task<IReadOnlyList<BudgetInfo>> GetBudgetsForResourceGroupAsync(
        TenantSubscription subscription, ResourceGroupInfo resourceGroup);

    Task<IReadOnlyList<BudgetCreationResult>> CreateBudgetForResourceGroupsAsync(
        TenantSubscription subscription,
        IEnumerable<ResourceGroupInfo> resourceGroups,
        BudgetFormModel form);

    Task DeleteBudgetAsync(BudgetInfo budget);
}
