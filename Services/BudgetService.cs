using Azure;
using Azure.ResourceManager.Consumption;
using Azure.ResourceManager.Consumption.Models;
using Azure.ResourceManager.Resources;
using FinOps.Models;

namespace FinOps.Services;

public class BudgetService(TenantClientManager tenantClientManager) : IBudgetService
{
    public async Task<IReadOnlyList<BudgetInfo>> GetBudgetsAsync(TenantSubscription subscription)
    {
        var client = tenantClientManager.GetClientForTenant(subscription.TenantId);
        var resourceId = SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId);
        var budgets = client.GetConsumptionBudgets(resourceId);

        var results = new List<BudgetInfo>();

        await foreach (var budget in budgets.GetAllAsync())
        {
            results.Add(new BudgetInfo
            {
                Name = budget.Data.Name,
                SubscriptionName = subscription.DisplayName,
                SubscriptionId = subscription.SubscriptionId,
                TenantId = subscription.TenantId,
                Amount = budget.Data.Amount,
                TimeGrain = budget.Data.TimeGrain?.ToString(),
                StartDate = budget.Data.TimePeriod?.StartOn,
                EndDate = budget.Data.TimePeriod?.EndOn,
                CurrentSpend = budget.Data.CurrentSpend?.Amount
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<BudgetInfo>> GetBudgetsForResourceGroupAsync(
        TenantSubscription subscription, ResourceGroupInfo resourceGroup)
    {
        var client = tenantClientManager.GetClientForTenant(subscription.TenantId);
        var resourceId = ResourceGroupResource.CreateResourceIdentifier(
            subscription.SubscriptionId, resourceGroup.Name);
        var budgets = client.GetConsumptionBudgets(resourceId);

        var results = new List<BudgetInfo>();

        await foreach (var budget in budgets.GetAllAsync())
        {
            results.Add(new BudgetInfo
            {
                Name = budget.Data.Name,
                SubscriptionName = subscription.DisplayName,
                SubscriptionId = subscription.SubscriptionId,
                TenantId = subscription.TenantId,
                ResourceGroupName = resourceGroup.Name,
                Amount = budget.Data.Amount,
                TimeGrain = budget.Data.TimeGrain?.ToString(),
                StartDate = budget.Data.TimePeriod?.StartOn,
                EndDate = budget.Data.TimePeriod?.EndOn,
                CurrentSpend = budget.Data.CurrentSpend?.Amount
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<BudgetCreationResult>> CreateBudgetForResourceGroupsAsync(
        TenantSubscription subscription,
        IEnumerable<ResourceGroupInfo> resourceGroups,
        BudgetFormModel form)
    {
        var results = new List<BudgetCreationResult>();

        foreach (var rg in resourceGroups)
        {
            try
            {
                var budgetData = new ConsumptionBudgetData
                {
                    Amount = form.Amount!.Value,
                    Category = BudgetCategory.Cost,
                    TimeGrain = ParseTimeGrain(form.TimeGrain),
                    TimePeriod = new BudgetTimePeriod(
                        new DateTimeOffset(form.StartDate!.Value, TimeSpan.Zero))
                    {
                        EndOn = form.EndDate.HasValue
                            ? new DateTimeOffset(form.EndDate.Value, TimeSpan.Zero)
                            : null
                    }
                };

                var client = tenantClientManager.GetClientForTenant(subscription.TenantId);
                var resourceId = ResourceGroupResource.CreateResourceIdentifier(
                    subscription.SubscriptionId, rg.Name);
                var budgets = client.GetConsumptionBudgets(resourceId);
                await budgets.CreateOrUpdateAsync(WaitUntil.Completed, form.Name, budgetData);

                results.Add(new BudgetCreationResult
                {
                    SubscriptionName = subscription.DisplayName,
                    SubscriptionId = subscription.SubscriptionId,
                    ResourceGroupName = rg.Name,
                    Success = true
                });
            }
            catch (RequestFailedException ex)
            {
                results.Add(new BudgetCreationResult
                {
                    SubscriptionName = subscription.DisplayName,
                    SubscriptionId = subscription.SubscriptionId,
                    ResourceGroupName = rg.Name,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<BudgetCreationResult>> CreateBudgetForSubscriptionsAsync(
        IEnumerable<TenantSubscription> subscriptions,
        BudgetFormModel form)
    {
        var results = new List<BudgetCreationResult>();

        foreach (var subscription in subscriptions)
        {
            var subName = subscription.DisplayName;
            var subId = subscription.SubscriptionId;

            try
            {
                var budgetData = new ConsumptionBudgetData
                {
                    Amount = form.Amount!.Value,
                    Category = BudgetCategory.Cost,
                    TimeGrain = ParseTimeGrain(form.TimeGrain),
                    TimePeriod = new BudgetTimePeriod(
                        new DateTimeOffset(form.StartDate!.Value, TimeSpan.Zero))
                    {
                        EndOn = form.EndDate.HasValue
                            ? new DateTimeOffset(form.EndDate.Value, TimeSpan.Zero)
                            : null
                    }
                };

                var client = tenantClientManager.GetClientForTenant(subscription.TenantId);
                var resourceId = SubscriptionResource.CreateResourceIdentifier(subId);
                var budgets = client.GetConsumptionBudgets(resourceId);
                await budgets.CreateOrUpdateAsync(WaitUntil.Completed, form.Name, budgetData);

                results.Add(new BudgetCreationResult
                {
                    SubscriptionName = subName,
                    SubscriptionId = subId,
                    Success = true
                });
            }
            catch (RequestFailedException ex)
            {
                results.Add(new BudgetCreationResult
                {
                    SubscriptionName = subName,
                    SubscriptionId = subId,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return results;
    }

    public async Task DeleteBudgetAsync(BudgetInfo budget)
    {
        var client = tenantClientManager.GetClientForTenant(budget.TenantId);
        var resourceId = budget.ResourceGroupName is not null
            ? ResourceGroupResource.CreateResourceIdentifier(budget.SubscriptionId, budget.ResourceGroupName)
            : SubscriptionResource.CreateResourceIdentifier(budget.SubscriptionId);
        var budgets = client.GetConsumptionBudgets(resourceId);
        var existing = await budgets.GetAsync(budget.Name);
        await existing.Value.DeleteAsync(WaitUntil.Completed);
    }

    private static BudgetTimeGrainType ParseTimeGrain(string timeGrain) => timeGrain switch
    {
        "Monthly" => BudgetTimeGrainType.Monthly,
        "Quarterly" => BudgetTimeGrainType.Quarterly,
        "Annually" => BudgetTimeGrainType.Annually,
        _ => BudgetTimeGrainType.Monthly
    };
}
