using FinOps.Models;
using Microsoft.Extensions.Logging;

namespace FinOps.Services;

public class AzureSubscriptionService(TenantClientManager tenantClientManager, ILogger<AzureSubscriptionService> logger) : IAzureSubscriptionService
{
    public async Task<IReadOnlyList<TenantSubscription>> GetSubscriptionsAsync()
    {
        var seen = new Dictionary<string, TenantSubscription>();
        var tenantDisplayNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (tenantId, client, isDefault) in tenantClientManager.GetAllClients())
        {
            logger.LogInformation("Enumerating subscriptions for tenant {TenantId} (isDefault: {IsDefault})", tenantId ?? "default", isDefault);
            try
            {
                // Build a cache of all tenant display names accessible via this client
                if (tenantDisplayNameCache.Count == 0 || isDefault)
                {
                    try
                    {
                        await foreach (var tenant in client.GetTenants().GetAllAsync())
                        {
                            var tid = tenant.Data.TenantId?.ToString();
                            var displayName = tenant.Data.DisplayName;
                            if (tid is not null && displayName is not null)
                            {
                                tenantDisplayNameCache[tid] = displayName;
                                logger.LogInformation("Cached tenant: {TenantId} -> {DisplayName}", tid, displayName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not load tenant display names");
                    }
                }

                await foreach (var subscription in client.GetSubscriptions().GetAllAsync())
                {
                    var subId = subscription.Data.SubscriptionId ?? "Unknown";
                    var subTenantId = subscription.Data.TenantId?.ToString() ?? "Unknown";

                    // Resolve the display name for THIS subscription's tenant
                    var subTenantDisplayName = tenantDisplayNameCache.TryGetValue(subTenantId, out var displayName)
                        ? displayName
                        : subTenantId;

                    logger.LogInformation("Found subscription: {Name} ({SubId}) in tenant {TenantName} ({TenantId})",
                        subscription.Data.DisplayName, subId, subTenantDisplayName, subTenantId);

                    var entry = new TenantSubscription
                    {
                        TenantId = subTenantId,
                        TenantDisplayName = subTenantDisplayName,
                        SubscriptionId = subId,
                        DisplayName = subscription.Data.DisplayName ?? subId,
                        State = subscription.Data.State?.ToString(),
                        IsDefault = isDefault
                    };

                    // Prefer explicit tenant client over default (more reliable for write operations)
                    if (!seen.ContainsKey(subId) || !isDefault)
                    {
                        seen[subId] = entry;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error enumerating subscriptions for tenant {TenantId}", tenantId ?? "default");
                throw;
            }
        }

        logger.LogInformation("Total subscriptions found: {Count}", seen.Count);
        return seen.Values.OrderBy(s => s.DisplayName).ToList().AsReadOnly();
    }
}
