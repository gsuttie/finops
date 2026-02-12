namespace FinOps.Models;

public class RetirementResource
{
    public required string ResourceId { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceGroup { get; init; }
    public required string Location { get; init; }
    public required string SubscriptionName { get; init; }
    public required string SubscriptionId { get; init; }
    public required string RetiringFeature { get; init; }
    public required string RetirementDate { get; init; }
}
