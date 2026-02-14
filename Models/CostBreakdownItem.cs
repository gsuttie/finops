namespace FinOps.Models;

public class CostBreakdownItem
{
    public required string Name { get; init; }
    public required decimal Cost { get; init; }
    public required string Currency { get; init; }
    public decimal Percentage { get; init; }
}
