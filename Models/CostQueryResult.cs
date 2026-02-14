namespace FinOps.Models;

public class CostQueryResult
{
    public required string SubscriptionId { get; init; }
    public required IReadOnlyList<CostDataPoint> Data { get; init; }
    public required decimal TotalCost { get; init; }
    public required string Currency { get; init; }
}
