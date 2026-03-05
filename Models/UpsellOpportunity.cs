namespace FinOps.Models;

public enum UpsellCategory { Security, CostOptimization, Modernization, Reliability, Governance }
public enum UpsellImpact { High, Medium, Low }

public class UpsellOpportunity
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required UpsellCategory Category { get; init; }
    public required UpsellImpact Impact { get; init; }
    public required string BusinessValue { get; init; }
    public required string TechnicalDetail { get; init; }
    public required string ResourceName { get; init; }
    public string? ResourceId { get; init; }
    public required string SubscriptionName { get; init; }
    public required string SubscriptionId { get; init; }
    public required string TenantId { get; init; }
    public required string Source { get; init; }
    public string? AzurePortalUrl { get; init; }
    public decimal? EstimatedMonthlySavings { get; init; }
}
