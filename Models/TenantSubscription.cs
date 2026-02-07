namespace FinOps.Models;

public class TenantSubscription
{
    public required string TenantId { get; init; }
    public string? TenantDisplayName { get; init; }
    public required string SubscriptionId { get; init; }
    public required string DisplayName { get; init; }
    public string? State { get; init; }
    public bool IsDefault { get; init; }
}
