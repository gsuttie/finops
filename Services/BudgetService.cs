using Azure;
using Azure.ResourceManager.Consumption;
using Azure.ResourceManager.Consumption.Models;
using Azure.ResourceManager.Resources;
using FinOps.Models;

namespace FinOps.Services;

public class BudgetService(TenantClientManager tenantClientManager) : IBudgetService
{
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

    private static BudgetTimeGrainType ParseTimeGrain(string timeGrain) => timeGrain switch
    {
        "Monthly" => BudgetTimeGrainType.Monthly,
        "Quarterly" => BudgetTimeGrainType.Quarterly,
        "Annually" => BudgetTimeGrainType.Annually,
        _ => BudgetTimeGrainType.Monthly
    };
}
