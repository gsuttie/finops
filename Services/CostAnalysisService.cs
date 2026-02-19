using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.CostManagement;
using Azure.ResourceManager.CostManagement.Models;
using Azure.ResourceManager.Resources;
using FinOps.Models;
using Microsoft.Extensions.Logging;

namespace FinOps.Services;

public class CostAnalysisService(
    TenantClientManager tenantClientManager,
    IBudgetService budgetService,
    IAdvisorService advisorService,
    ILogger<CostAnalysisService> logger) : ICostAnalysisService
{
    private const int MaxRetries = 3;
    private const int BaseDelayMs = 1000;

    private static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (RequestFailedException ex) when (ex.Status == 429 && attempt < MaxRetries - 1)
            {
                // Calculate exponential backoff delay
                var delayMs = BaseDelayMs * (int)Math.Pow(2, attempt);

                // Try to get Retry-After header
                var rawResponse = ex.GetRawResponse();
                if (rawResponse != null && rawResponse.Headers.TryGetValue("Retry-After", out var retryAfterValue))
                {
                    if (int.TryParse(retryAfterValue, out var seconds))
                    {
                        delayMs = seconds * 1000;
                    }
                }

                await Task.Delay(delayMs);
            }
        }

        // Final attempt without catching
        return await operation();
    }

    public async Task<CostQueryResult> GetCostsAsync(
        string subscriptionId,
        string tenantId,
        DateTime startDate,
        DateTime endDate)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var client = tenantClientManager.GetClientForTenant(tenantId);
            var scope = SubscriptionResource.CreateResourceIdentifier(subscriptionId);

            var dataset = new QueryDataset()
            {
                Granularity = GranularityType.Daily,
                Aggregation =
                {
                    ["totalCost"] = new QueryAggregation("PreTaxCost", FunctionType.Sum)
                }
            };

            var queryDefinition = new QueryDefinition(
                ExportType.ActualCost,
                TimeframeType.Custom,
                dataset)
            {
                TimePeriod = new QueryTimePeriod(
                    new DateTimeOffset(startDate, TimeSpan.Zero),
                    new DateTimeOffset(endDate, TimeSpan.Zero))
            };

            var response = await client.UsageQueryAsync(scope, queryDefinition);
            return TransformQueryResponse(subscriptionId, response.Value);
        });
    }

    public async Task<IReadOnlyList<CostBreakdownItem>> GetCostsByDimensionAsync(
        string subscriptionId,
        string tenantId,
        DateTime startDate,
        DateTime endDate,
        string dimension,
        int topN = 5)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var client = tenantClientManager.GetClientForTenant(tenantId);
            var scope = SubscriptionResource.CreateResourceIdentifier(subscriptionId);

            var dataset = new QueryDataset()
            {
                Granularity = null, // No granularity for total aggregation
                Aggregation =
                {
                    ["totalCost"] = new QueryAggregation("PreTaxCost", FunctionType.Sum)
                },
                Grouping =
                {
                    new QueryGrouping(QueryColumnType.Dimension, dimension)
                }
            };

            var queryDefinition = new QueryDefinition(
                ExportType.ActualCost,
                TimeframeType.Custom,
                dataset)
            {
                TimePeriod = new QueryTimePeriod(
                    new DateTimeOffset(startDate, TimeSpan.Zero),
                    new DateTimeOffset(endDate, TimeSpan.Zero))
            };

            var response = await client.UsageQueryAsync(scope, queryDefinition);
            return TransformToBreakdownItems(response.Value, topN);
        });
    }

    public async Task<IReadOnlyList<CostBreakdownItem>> GetCostsByTagKeyAsync(
        string subscriptionId,
        string tenantId,
        DateTime startDate,
        DateTime endDate,
        string tagKey,
        int topN = 50)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var client = tenantClientManager.GetClientForTenant(tenantId);
            var scope = SubscriptionResource.CreateResourceIdentifier(subscriptionId);

            var dataset = new QueryDataset()
            {
                Granularity = null,
                Aggregation =
                {
                    ["totalCost"] = new QueryAggregation("PreTaxCost", FunctionType.Sum)
                },
                Grouping =
                {
                    new QueryGrouping(QueryColumnType.TagKey, tagKey)
                }
            };

            var queryDefinition = new QueryDefinition(
                ExportType.ActualCost,
                TimeframeType.Custom,
                dataset)
            {
                TimePeriod = new QueryTimePeriod(
                    new DateTimeOffset(startDate, TimeSpan.Zero),
                    new DateTimeOffset(endDate, TimeSpan.Zero))
            };

            var response = await client.UsageQueryAsync(scope, queryDefinition);
            return TransformToBreakdownItems(response.Value, topN);
        });
    }

    public async Task<Models.ForecastResult> GetCostForecastAsync(
        string subscriptionId,
        string tenantId,
        DateTime startDate,
        DateTime endDate)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var client = tenantClientManager.GetClientForTenant(tenantId);
            var scope = SubscriptionResource.CreateResourceIdentifier(subscriptionId);

            var aggregation = new Dictionary<string, ForecastAggregation>
            {
                ["totalCost"] = new ForecastAggregation("Cost", FunctionType.Sum)
            };

            var dataset = new ForecastDataset(aggregation)
            {
                Granularity = GranularityType.Daily
            };

            var forecastDefinition = new ForecastDefinition(
                ForecastType.ActualCost,
                ForecastTimeframe.Custom,
                dataset)
            {
                TimePeriod = new ForecastTimePeriod(
                    new DateTimeOffset(startDate, TimeSpan.Zero),
                    new DateTimeOffset(endDate, TimeSpan.Zero)),
                IncludeActualCost = true,
                IncludeFreshPartialCost = true
            };

            var response = await client.UsageForecastAsync(scope, forecastDefinition);
            return TransformForecastResponse(subscriptionId, response);
        });
    }

    public async Task<CostDashboardData> GetDashboardDataAsync(TenantSubscription subscription)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var thirtyDaysAgo = now.AddDays(-30);

        // Execute queries in batches to avoid rate limiting
        // Batch 1: Basic costs and budget (2-3 queries)
        var currentSpendTask = GetCurrentMonthSpendAsync(subscription, monthStart, now);
        var budgetsTask = budgetService.GetBudgetsAsync(subscription);

        await Task.WhenAll(currentSpendTask, budgetsTask);
        var currentSpend = await currentSpendTask;
        var budgets = await budgetsTask;

        // Small delay to avoid rate limiting
        await Task.Delay(500);

        // Batch 2: Forecast and trend (2 queries)
        var forecastTask = GetMonthForecastAsync(subscription, now, monthEnd);
        var trendTask = GetCostsAsync(subscription.SubscriptionId, subscription.TenantId, thirtyDaysAgo, now);

        await Task.WhenAll(forecastTask, trendTask);
        var forecast = await forecastTask;
        var trend = await trendTask;

        // Small delay to avoid rate limiting
        await Task.Delay(500);

        // Batch 3: Cost breakdowns (3 queries) - run sequentially to be safer
        // Using correct Azure Cost Management dimension names
        var topServices = await GetTopCostDriversAsync(subscription, monthStart, now, "ServiceName", 5);
        await Task.Delay(300);
        var topResourceGroups = await GetTopCostDriversAsync(subscription, monthStart, now, "ResourceGroup", 5);
        await Task.Delay(300);
        var topLocations = await GetTopCostDriversAsync(subscription, monthStart, now, "ResourceLocation", 5);

        // Get potential savings from Advisor
        var potentialSavings = await GetPotentialSavingsAsync(subscription);

        // Get budget info (take first budget if multiple exist)
        var budget = budgets.FirstOrDefault();
        var budgetAmount = budget?.Amount;
        var budgetRemaining = budget != null && budget.CurrentSpend.HasValue
            ? budget.Amount - budget.CurrentSpend.Value
            : null;

        return new CostDashboardData
        {
            SubscriptionId = subscription.SubscriptionId,
            SubscriptionName = subscription.DisplayName,
            CurrentMonthSpend = currentSpend,
            MonthForecast = forecast,
            Currency = trend.Currency,
            BudgetAmount = budgetAmount,
            BudgetRemaining = budgetRemaining,
            PotentialSavings = potentialSavings > 0 ? potentialSavings : null,
            TopServices = topServices,
            TopResourceGroups = topResourceGroups,
            TopLocations = topLocations,
            TrendData = trend.Data
        };
    }

    private async Task<decimal> GetCurrentMonthSpendAsync(
        TenantSubscription subscription,
        DateTime monthStart,
        DateTime now)
    {
        try
        {
            var result = await GetCostsAsync(
                subscription.SubscriptionId,
                subscription.TenantId,
                monthStart,
                now);
            return result.TotalCost;
        }
        catch
        {
            return 0m;
        }
    }

    private async Task<decimal> GetMonthForecastAsync(
        TenantSubscription subscription,
        DateTime now,
        DateTime monthEnd)
    {
        try
        {
            var result = await GetCostForecastAsync(
                subscription.SubscriptionId,
                subscription.TenantId,
                now,
                monthEnd);
            return result.TotalForecast;
        }
        catch
        {
            return 0m;
        }
    }

    private async Task<IReadOnlyList<CostBreakdownItem>> GetTopCostDriversAsync(
        TenantSubscription subscription,
        DateTime startDate,
        DateTime endDate,
        string dimension,
        int topN)
    {
        try
        {
            return await GetCostsByDimensionAsync(
                subscription.SubscriptionId,
                subscription.TenantId,
                startDate,
                endDate,
                dimension,
                topN);
        }
        catch
        {
            return Array.Empty<CostBreakdownItem>();
        }
    }

    private async Task<decimal> GetPotentialSavingsAsync(TenantSubscription subscription)
    {
        try
        {
            return await advisorService.GetPotentialCostSavingsAsync(new[] { subscription });
        }
        catch
        {
            return 0m;
        }
    }

    private static CostQueryResult TransformQueryResponse(string subscriptionId, QueryResult queryResult)
    {
        var dataPoints = new List<CostDataPoint>();
        var currency = "USD";

        if (queryResult.Rows is { Count: > 0 } && queryResult.Columns is { Count: > 0 })
        {
            // Find column indices
            var dateIndex = FindColumnIndex(queryResult.Columns, "UsageDate");
            var costIndex = FindColumnIndex(queryResult.Columns, "PreTaxCost");
            var currencyIndex = FindColumnIndex(queryResult.Columns, "Currency");

            foreach (var row in queryResult.Rows)
            {
                if (row.Count > Math.Max(dateIndex, costIndex))
                {
                    var date = ParseDate(row[dateIndex]);
                    var cost = ParseDecimal(row[costIndex]);

                    if (currencyIndex >= 0 && currencyIndex < row.Count)
                    {
                        currency = row[currencyIndex]?.ToString() ?? "USD";
                    }

                    dataPoints.Add(new CostDataPoint
                    {
                        Date = date,
                        Cost = cost,
                        Currency = currency
                    });
                }
            }
        }

        var totalCost = dataPoints.Sum(d => d.Cost);

        return new CostQueryResult
        {
            SubscriptionId = subscriptionId,
            Data = dataPoints,
            TotalCost = totalCost,
            Currency = currency
        };
    }

    private IReadOnlyList<CostBreakdownItem> TransformToBreakdownItems(
        QueryResult queryResult,
        int topN)
    {
        var items = new List<CostBreakdownItem>();
        var currency = "USD";

        if (queryResult.Rows is { Count: > 0 } && queryResult.Columns is { Count: > 0 })
        {
            // Log column names for debugging
            logger.LogInformation("Cost breakdown columns: {Columns}",
                string.Join(", ", queryResult.Columns.Select(c => c.Name)));

            // Find the dimension column (first column that's not Cost or Currency)
            var nameIndex = 0;
            for (int i = 0; i < queryResult.Columns.Count; i++)
            {
                var colName = queryResult.Columns[i].Name;
                if (colName != "PreTaxCost" && colName != "Cost" && colName != "Currency")
                {
                    nameIndex = i;
                    logger.LogInformation("Using column '{ColumnName}' at index {Index} for dimension names", colName, i);
                    break;
                }
            }

            var costIndex = FindColumnIndex(queryResult.Columns, "PreTaxCost");
            var currencyIndex = FindColumnIndex(queryResult.Columns, "Currency");

            logger.LogInformation("Found {RowCount} rows in cost breakdown", queryResult.Rows.Count);

            foreach (var row in queryResult.Rows)
            {
                if (row.Count > Math.Max(nameIndex, costIndex))
                {
                    var nameValue = row[nameIndex];
                    var name = nameValue?.ToString() ?? "Unknown";

                    // Skip if name is empty or null
                    if (string.IsNullOrWhiteSpace(name) || name == "Unknown")
                        continue;

                    var cost = ParseDecimal(row[costIndex]);

                    if (currencyIndex >= 0 && currencyIndex < row.Count)
                    {
                        currency = row[currencyIndex]?.ToString() ?? "USD";
                    }

                    items.Add(new CostBreakdownItem
                    {
                        Name = name,
                        Cost = cost,
                        Currency = currency,
                        Percentage = 0 // Will be calculated after sorting
                    });
                }
            }
        }

        // Sort by cost descending and take top N
        items = items.OrderByDescending(i => i.Cost).Take(topN).ToList();

        // Calculate percentages
        var total = items.Sum(i => i.Cost);
        if (total > 0)
        {
            items = items.Select(i => new CostBreakdownItem
            {
                Name = i.Name,
                Cost = i.Cost,
                Currency = i.Currency,
                Percentage = Math.Round((i.Cost / total) * 100, 1)
            }).ToList();
        }

        return items;
    }

    private static Models.ForecastResult TransformForecastResponse(
        string subscriptionId,
        Azure.ResourceManager.CostManagement.Models.ForecastResult forecastResult)
    {
        var dataPoints = new List<ForecastDataPoint>();
        var currency = "USD";
        var totalForecast = 0m;

        if (forecastResult.Rows is { Count: > 0 } && forecastResult.Columns is { Count: > 0 })
        {
            var dateIndex = FindForecastColumnIndex(forecastResult.Columns, "UsageDate");
            var costIndex = FindForecastColumnIndex(forecastResult.Columns, "Cost");
            var costStatusIndex = FindForecastColumnIndex(forecastResult.Columns, "CostStatus");
            var currencyIndex = FindForecastColumnIndex(forecastResult.Columns, "Currency");

            foreach (var row in forecastResult.Rows)
            {
                if (row.Count > Math.Max(dateIndex, costIndex))
                {
                    var date = ParseDate(row[dateIndex]);
                    var cost = ParseDecimal(row[costIndex]);
                    var costStatus = costStatusIndex >= 0 && costStatusIndex < row.Count
                        ? row[costStatusIndex]?.ToString()
                        : null;

                    if (currencyIndex >= 0 && currencyIndex < row.Count)
                    {
                        currency = row[currencyIndex]?.ToString() ?? "USD";
                    }

                    var isActual = costStatus?.Equals("Actual", StringComparison.OrdinalIgnoreCase) == true;

                    dataPoints.Add(new ForecastDataPoint
                    {
                        Date = date,
                        ActualCost = isActual ? cost : null,
                        ForecastedCost = isActual ? null : cost,
                        UpperBound = isActual ? null : cost * 1.1m, // 10% confidence interval
                        LowerBound = isActual ? null : cost * 0.9m,
                        Currency = currency
                    });

                    if (!isActual)
                    {
                        totalForecast += cost;
                    }
                }
            }
        }

        return new Models.ForecastResult
        {
            SubscriptionId = subscriptionId,
            Data = dataPoints,
            TotalForecast = totalForecast,
            Currency = currency
        };
    }

    private static int FindColumnIndex(IReadOnlyList<QueryColumn> columns, string? name, int defaultIndex = -1)
    {
        if (name is null)
            return defaultIndex;

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                return i;
        }

        return defaultIndex;
    }

    private static int FindForecastColumnIndex(IReadOnlyList<ForecastColumn> columns, string? name, int defaultIndex = -1)
    {
        if (name is null)
            return defaultIndex;

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                return i;
        }

        return defaultIndex;
    }

    private static DateTime ParseDate(object? value)
    {
        if (value is null)
            return DateTime.UtcNow;

        if (value is DateTime dt)
            return dt;

        if (value is DateTimeOffset dto)
            return dto.UtcDateTime;

        if (int.TryParse(value.ToString(), out var dateInt))
        {
            // Cost Management API returns dates as YYYYMMDD integers
            var year = dateInt / 10000;
            var month = (dateInt / 100) % 100;
            var day = dateInt % 100;
            return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        }

        return DateTime.TryParse(value.ToString(), out var parsed)
            ? parsed
            : DateTime.UtcNow;
    }

    private static decimal ParseDecimal(object? value)
    {
        if (value is null)
            return 0m;

        if (value is decimal d)
            return d;

        return decimal.TryParse(value.ToString(), out var result)
            ? result
            : 0m;
    }
}
