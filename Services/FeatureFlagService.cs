using System.Text.Json;
using FinOps.Models;

namespace FinOps.Services;

public class FeatureFlagService : IFeatureFlagService
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "featureflags.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public FeatureFlags Flags { get; private set; }
    public event Action? OnFlagsChanged;

    public FeatureFlagService()
    {
        Flags = Load();
    }

    public async Task SaveAsync(FeatureFlags flags)
    {
        Flags = flags;
        var json = JsonSerializer.Serialize(flags, JsonOptions);
        await File.WriteAllTextAsync(FilePath, json);
        OnFlagsChanged?.Invoke();
    }

    private static FeatureFlags Load()
    {
        if (!File.Exists(FilePath))
            return new FeatureFlags();

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<FeatureFlags>(json) ?? new FeatureFlags();
        }
        catch
        {
            return new FeatureFlags();
        }
    }
}
