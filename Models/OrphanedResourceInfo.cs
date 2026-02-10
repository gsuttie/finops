namespace FinOps.Models;

public class OrphanedResourceInfo
{
    public required string ResourceId { get; init; }
    public required string Name { get; init; }
    public required string ResourceType { get; init; }
    public required string Category { get; init; }
    public required string ResourceGroup { get; init; }
    public required string Location { get; init; }
    public required string SubscriptionName { get; init; }
    public required string SubscriptionId { get; init; }
    public required string TenantId { get; init; }
}
