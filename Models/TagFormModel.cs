namespace FinOps.Models;

public class TagEntry
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class TagFormModel
{
    public List<TagEntry> Tags { get; set; } = [new()];
}
