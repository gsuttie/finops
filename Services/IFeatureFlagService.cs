using FinOps.Models;

namespace FinOps.Services;

public interface IFeatureFlagService
{
    FeatureFlags Flags { get; }
    Task SaveAsync(FeatureFlags flags);
    event Action? OnFlagsChanged;
}
