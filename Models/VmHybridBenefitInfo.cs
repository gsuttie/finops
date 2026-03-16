namespace FinOps.Models;

public class VmHybridBenefitInfo
{
    public required string ResourceId { get; init; }
    public required string Name { get; init; }
    public required string ResourceGroup { get; init; }
    public required string Location { get; init; }
    public required string SubscriptionId { get; init; }
    public required string SubscriptionName { get; init; }
    public required string TenantId { get; init; }
    public string VmSize { get; init; } = "";
    public string? PowerState { get; init; }
    public bool HybridBenefitEnabled { get; init; }
}
