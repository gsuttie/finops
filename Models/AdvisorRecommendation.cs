namespace FinOps.Models;

public class AdvisorRecommendation
{
    public required string RecommendationId { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Impact { get; init; }
    public required string Problem { get; init; }
    public required string Solution { get; init; }
    public required string ImpactedField { get; init; }
    public required string ImpactedValue { get; init; }
    public required string ResourceId { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }
    public required string SubscriptionName { get; init; }
    public required string SubscriptionId { get; init; }
    public required string TenantId { get; init; }
}
