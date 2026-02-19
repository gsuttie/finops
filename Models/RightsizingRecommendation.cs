namespace FinOps.Models;

public class RightsizingRecommendation
{
    public required string ResourceId { get; init; }
    public required string ResourceName { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceGroup { get; init; }
    public required string Location { get; init; }
    public required string SubscriptionId { get; init; }
    public required string SubscriptionName { get; init; }
    public required string TenantId { get; init; }
    public required string RecommendationTitle { get; init; }
    public required string ImpactLevel { get; init; }       // High / Medium / Low
    public required string Category { get; init; }          // e.g. "Cost"
    public string? CurrentSku { get; init; }                // e.g. "Standard_D4s_v3"
    public string? RecommendedAction { get; init; }         // e.g. "Resize to Standard_D2s_v3" or "Shutdown"
    public decimal? PotentialSavingsMonthly { get; init; }  // USD
}
