using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using FinOps.Models;

namespace FinOps.Services;

public class ResourceTaggingService(TenantClientManager tenantClientManager) : IResourceTaggingService
{
    public async Task<IReadOnlyList<ResourceGroupInfo>> GetResourceGroupsAsync(TenantSubscription subscription)
    {
        var client = tenantClientManager.GetClientForTenant(subscription.TenantId);
        var subResource = client.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));

        var resourceGroups = new List<ResourceGroupInfo>();

        await foreach (var rg in subResource.GetResourceGroups().GetAllAsync())
        {
            resourceGroups.Add(new ResourceGroupInfo
            {
                Name = rg.Data.Name,
                Id = rg.Data.Id.ToString(),
                Location = rg.Data.Location.DisplayName,
                ProvisioningState = rg.Data.ResourceGroupProvisioningState,
                Tags = rg.Data.Tags != null
                    ? new Dictionary<string, string>(rg.Data.Tags)
                    : new Dictionary<string, string>()
            });
        }

        return resourceGroups;
    }

    public async Task<IReadOnlyList<TagOperationResult>> ApplyTagsAsync(
        TenantSubscription subscription,
        IEnumerable<ResourceGroupInfo> resourceGroups,
        IReadOnlyDictionary<string, string> tags,
        Action<TagOperationResult>? onProgress = null)
    {
        var client = tenantClientManager.GetClientForTenant(subscription.TenantId);
        var subResource = client.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));

        var patch = new TagResourcePatch
        {
            PatchMode = TagPatchMode.Merge
        };
        foreach (var (key, value) in tags)
        {
            patch.TagValues.Add(key, value);
        }

        var results = new List<TagOperationResult>();

        foreach (var rgInfo in resourceGroups)
        {
            var rgResource = (await subResource.GetResourceGroups().GetAsync(rgInfo.Name)).Value;

            // Tag the resource group itself
            var rgResult = await TagResourceSafe(rgResource.GetTagResource(), patch, rgInfo.Name, "ResourceGroup");
            results.Add(rgResult);
            onProgress?.Invoke(rgResult);

            // Tag all resources within the resource group
            await foreach (var resource in rgResource.GetGenericResourcesAsync())
            {
                var resourceResult = await TagResourceSafe(
                    resource.GetTagResource(), patch, resource.Data.Name, resource.Data.ResourceType.ToString());
                results.Add(resourceResult);
                onProgress?.Invoke(resourceResult);
            }
        }

        return results;
    }

    private static async Task<TagOperationResult> TagResourceSafe(
        TagResource tagResource, TagResourcePatch patch, string name, string type)
    {
        try
        {
            await tagResource.UpdateAsync(WaitUntil.Completed, patch);
            return new TagOperationResult
            {
                ResourceName = name,
                ResourceType = type,
                Success = true
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return new TagOperationResult
            {
                ResourceName = name,
                ResourceType = type,
                Success = false,
                ErrorMessage = "Skipped — resource has a background operation in progress"
            };
        }
        catch (RequestFailedException ex)
        {
            return new TagOperationResult
            {
                ResourceName = name,
                ResourceType = type,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
