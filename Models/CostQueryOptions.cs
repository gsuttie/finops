namespace FinOps.Models;

public class CostQueryOptions
{
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public string? Granularity { get; init; } = "Daily";
    public string? GroupBy { get; init; }
}
