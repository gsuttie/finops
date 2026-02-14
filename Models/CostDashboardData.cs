namespace FinOps.Models;

public class CostDashboardData
{
    public required string SubscriptionId { get; init; }
    public required string SubscriptionName { get; init; }

    // KPI metrics
    public required decimal CurrentMonthSpend { get; init; }
    public required decimal MonthForecast { get; init; }
    public required string Currency { get; init; }

    // Budget info (if exists)
    public decimal? BudgetAmount { get; init; }
    public decimal? BudgetRemaining { get; init; }

    // Potential savings
    public decimal? PotentialSavings { get; init; }

    // Cost breakdowns (top 5 each)
    public required IReadOnlyList<CostBreakdownItem> TopServices { get; init; }
    public required IReadOnlyList<CostBreakdownItem> TopResourceGroups { get; init; }
    public required IReadOnlyList<CostBreakdownItem> TopLocations { get; init; }

    // Trend data (last 30 days)
    public required IReadOnlyList<CostDataPoint> TrendData { get; init; }
}
