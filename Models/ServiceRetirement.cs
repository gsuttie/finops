namespace FinOps.Models;

public class ServiceRetirement
{
    public required string ServiceName { get; init; }
    public required string RetiringFeature { get; init; }
    public required string RetirementDate { get; init; }
    public required int ResourceCount { get; init; }
    public required string Description { get; init; }
    public required string SubscriptionName { get; init; }
    public required string SubscriptionId { get; init; }
    public required string TenantId { get; init; }
    public required List<RetirementResource> Resources { get; init; }
}
