using FinOps.Models;

namespace FinOps.Services;

public interface IResourceTaggingService
{
    Task<IReadOnlyList<ResourceGroupInfo>> GetResourceGroupsAsync(TenantSubscription subscription);

    Task<IReadOnlyList<TagOperationResult>> ApplyTagsAsync(
        TenantSubscription subscription,
        IEnumerable<ResourceGroupInfo> resourceGroups,
        IReadOnlyDictionary<string, string> tags,
        Action<TagOperationResult>? onProgress = null);
}
