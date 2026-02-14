namespace FinOps.Models;

public class ForecastResult
{
    public required string SubscriptionId { get; init; }
    public required IReadOnlyList<ForecastDataPoint> Data { get; init; }
    public required decimal TotalForecast { get; init; }
    public required string Currency { get; init; }
}
