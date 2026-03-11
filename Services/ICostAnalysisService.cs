using FinOps.Models;

namespace FinOps.Services;

public interface ICostAnalysisService
{
    /// <summary>
    /// Queries historical/current costs for a subscription within a date range
    /// </summary>
    Task<CostQueryResult> GetCostsAsync(
        string subscriptionId,
        string tenantId,
        DateTime startDate,
        DateTime endDate);

    /// <summary>
    /// Gets cost breakdown by a specific dimension (service, resource group, location, etc.)
    /// </summary>
    Task<IReadOnlyList<CostBreakdownItem>> GetCostsByDimensionAsync(
        string subscriptionId,
        string tenantId,
        DateTime startDate,
        DateTime endDate,
        string dimension,
        int topN = 5);

    /// <summary>
    /// Gets cost forecast for a subscription
    /// </summary>
    Task<Models.ForecastResult> GetCostForecastAsync(
        string subscriptionId,
        string tenantId,
        DateTime startDate,
        DateTime endDate);

    /// <summary>
    /// Gets cost breakdown by a specific tag key (grouped by tag values)
    /// </summary>
    Task<IReadOnlyList<CostBreakdownItem>> GetCostsByTagKeyAsync(
        string subscriptionId,
        string tenantId,
        DateTime startDate,
        DateTime endDate,
        string tagKey,
        int topN = 50);

    /// <summary>
    /// Gets comprehensive dashboard data with parallel queries
    /// </summary>
    Task<CostDashboardData> GetDashboardDataAsync(
        TenantSubscription subscription);

    /// <summary>
    /// Analyses spend trends: current month vs previous month and vs 3-month average, with anomaly detection
    /// </summary>
    Task<SpendTrendAnalysis> GetSpendTrendAnalysisAsync(TenantSubscription subscription);
}
