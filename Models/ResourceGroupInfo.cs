namespace FinOps.Models;

public class ResourceGroupInfo
{
    public required string Name { get; init; }
    public required string Id { get; init; }
    public string? Location { get; init; }
    public string? ProvisioningState { get; init; }
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
}
