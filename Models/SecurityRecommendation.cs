namespace FinOps.Models;

public class SecurityRecommendation
{
    public required string AssessmentId { get; init; }
    public required string RecommendationName { get; init; }
    public required string Severity { get; init; }
    public required string Status { get; init; }
    public required string Category { get; init; }
    public required string Description { get; init; }
    public required string ResourceName { get; init; }
    public required string ResourceId { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceGroup { get; init; }
    public required string SubscriptionName { get; init; }
    public required string SubscriptionId { get; init; }
    public required string TenantId { get; init; }
}
