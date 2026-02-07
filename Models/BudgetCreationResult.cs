namespace FinOps.Models;

public class BudgetCreationResult
{
    public required string SubscriptionName { get; init; }
    public required string SubscriptionId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
