namespace FinOps.Models;

public class PolicyAssignmentInfo
{
    public required string Name { get; init; }
    public required string Id { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? PolicyDefinitionId { get; init; }
    public string? TagName { get; init; }
    public string? TagValue { get; init; }
    public string? EnforcementMode { get; init; }
    public bool HasManagedIdentity { get; init; }
}
