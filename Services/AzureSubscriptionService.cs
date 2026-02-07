using FinOps.Models;
using Microsoft.Extensions.Logging;

namespace FinOps.Services;

public class AzureSubscriptionService(TenantClientManager tenantClientManager, ILogger<AzureSubscriptionService> logger) : IAzureSubscriptionService
{
    public async Task<IReadOnlyList<TenantSubscription>> GetSubscriptionsAsync()
    {
        var seen = new Dictionary<string, TenantSubscription>();

        foreach (var (tenantId, client, isDefault) in tenantClientManager.GetAllClients())
        {
            logger.LogInformation("Enumerating subscriptions for tenant {TenantId} (isDefault: {IsDefault})", tenantId ?? "default", isDefault);
            try
            {
                await foreach (var subscription in client.GetSubscriptions().GetAllAsync())
                {
                    var subId = subscription.Data.SubscriptionId ?? "Unknown";
                    var subTenantId = tenantId ?? subscription.Data.TenantId?.ToString() ?? "Unknown";
                    logger.LogInformation("Found subscription: {Name} ({SubId})", subscription.Data.DisplayName, subId);

                    var entry = new TenantSubscription
                    {
                        TenantId = subTenantId,
                        TenantDisplayName = isDefault ? "Home / Lighthouse" : subTenantId,
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
