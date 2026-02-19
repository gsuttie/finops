namespace FinOps.Models;

public class PrivateEndpointRecommendation
{
    public required string ResourceId { get; init; }
    public required string ResourceName { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceGroup { get; init; }
    public required string Location { get; init; }
    public required string SubscriptionId { get; init; }
    public required string SubscriptionName { get; init; }
    public required string TenantId { get; init; }
    public required string CurrentState { get; init; }
    public required string RecommendationReason { get; init; }
    public List<string> SupportedPrivateEndpointTypes { get; init; } = [];
    public bool HasPublicAccess { get; init; }
    public string? NetworkAcls { get; init; }
}
