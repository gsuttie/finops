namespace FinOps.Models;

public class CostDataPoint
{
    public required DateTime Date { get; init; }
    public required decimal Cost { get; init; }
    public required string Currency { get; init; }
}
