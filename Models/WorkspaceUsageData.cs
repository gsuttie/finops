namespace FinOps.Models;

public class WorkspaceUsageData
{
    private const double CostPerGb = 2.76;

    public required string WorkspaceName { get; init; }
    public required string CustomerId { get; init; }
    public required IReadOnlyList<DailyUsage> DailyIngestion { get; init; }
    public required IReadOnlyList<DataTypeUsage> UsageByType { get; init; }

    public double TotalDataGb => UsageByType.Sum(u => u.DataGb);
    public double EstimatedCost => TotalDataGb * CostPerGb;
}

public class DailyUsage
{
    public required DateTimeOffset Date { get; init; }
    public required double DataGb { get; init; }
}

public class DataTypeUsage
{
    private const double CostPerGb = 2.76;

    public required string DataType { get; init; }
    public required double DataGb { get; init; }
    public double EstimatedCost => DataGb * CostPerGb;
}
