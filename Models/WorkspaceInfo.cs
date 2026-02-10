namespace FinOps.Models;

public class WorkspaceInfo
{
    public required string WorkspaceResourceId { get; init; }
    public required string Name { get; init; }
    public required string CustomerId { get; init; }
    public required string ResourceGroup { get; init; }
    public required string Location { get; init; }
    public required string SkuName { get; init; }
    public required int RetentionInDays { get; init; }
    public double? DailyQuotaGb { get; init; }
    public required string SubscriptionName { get; init; }
    public required string SubscriptionId { get; init; }
    public required string TenantId { get; init; }
}
