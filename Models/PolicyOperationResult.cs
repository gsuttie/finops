namespace FinOps.Models;

public class PolicyOperationResult
{
    public required string SubscriptionName { get; init; }
    public required string SubscriptionId { get; init; }
    public required string Operation { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? PolicyAssignmentName { get; init; }
}
