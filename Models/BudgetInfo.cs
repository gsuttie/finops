namespace FinOps.Models;

public class BudgetInfo
{
    public required string Name { get; init; }
    public required string SubscriptionName { get; init; }
    public required string SubscriptionId { get; init; }
    public required string TenantId { get; init; }
    public string? ResourceGroupName { get; init; }
    public decimal? Amount { get; init; }
    public string? TimeGrain { get; init; }
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public decimal? CurrentSpend { get; init; }
}
