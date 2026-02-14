namespace FinOps.Models;

public class ForecastDataPoint
{
    public required DateTime Date { get; init; }
    public decimal? ActualCost { get; init; }
    public decimal? ForecastedCost { get; init; }
    public decimal? UpperBound { get; init; }
    public decimal? LowerBound { get; init; }
    public required string Currency { get; init; }
}
