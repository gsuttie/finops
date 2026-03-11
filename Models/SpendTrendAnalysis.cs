namespace FinOps.Models;

public class SpendTrendAnalysis
{
    public string SubscriptionId { get; set; } = "";
    public string SubscriptionName { get; set; } = "";
    public string Currency { get; set; } = "USD";

    public decimal CurrentMonthTotal { get; set; }
    public decimal PreviousMonthTotal { get; set; }
    public decimal ThreeMonthAverage { get; set; }

    // % change (positive = more expensive, negative = cheaper)
    public decimal ChangeVsPreviousMonth { get; set; }
    public decimal ChangeVsThreeMonthAverage { get; set; }

    public IReadOnlyList<CostDataPoint> CurrentMonthDailyData { get; set; } = [];
    public IReadOnlyList<CostDataPoint> PreviousMonthDailyData { get; set; } = [];
    public IReadOnlyList<CostDataPoint> ThreeMonthDailyData { get; set; } = [];

    public IReadOnlyList<SpendAnomaly> Anomalies { get; set; } = [];
}
