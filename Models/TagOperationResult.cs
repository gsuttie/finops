namespace FinOps.Models;

public class TagOperationResult
{
    public required string ResourceName { get; init; }
    public required string ResourceType { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
