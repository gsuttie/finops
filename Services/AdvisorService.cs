using Azure.Core;
using Azure.ResourceManager.Advisor;
using Azure.ResourceManager.Resources;
using FinOps.Models;

namespace FinOps.Services;

public class AdvisorService(TenantClientManager tenantClientManager) : IAdvisorService
{
    public async Task<IReadOnlyList<AdvisorRecommendation>> GetRecommendationsAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subList = subscriptions.ToList();
        if (subList.Count == 0)
            return [];

        var tenantGroups = subList.GroupBy(s => s.TenantId);

        var tenantTasks = tenantGroups.Select(group =>
            ProcessTenantAsync(group.Key, group.ToList()));
        var tenantResults = await Task.WhenAll(tenantTasks);

        return tenantResults.SelectMany(r => r).ToList();
    }

    private async Task<List<AdvisorRecommendation>> ProcessTenantAsync(
        string tenantId,
        List<TenantSubscription> subscriptions)
    {
        var client = tenantClientManager.GetClientForTenant(tenantId);

        var subTasks = subscriptions.Select(sub =>
            ProcessSubscriptionAsync(client, sub));
        var subResults = await Task.WhenAll(subTasks);

        return subResults.SelectMany(r => r).ToList();
    }

    private static async Task<List<AdvisorRecommendation>> ProcessSubscriptionAsync(
        Azure.ResourceManager.ArmClient client,
        TenantSubscription sub)
    {
        var results = new List<AdvisorRecommendation>();

        var scope = new ResourceIdentifier($"/subscriptions/{sub.SubscriptionId}");
        var collection = client.GetResourceRecommendationBases(scope);

        await foreach (var rec in collection.GetAllAsync())
        {
            var data = rec.Data;

            results.Add(new AdvisorRecommendation
            {
                RecommendationId = rec.Id.ToString(),
                Name = data.Name ?? "",
                Category = data.Category?.ToString() ?? "Unknown",
                Impact = data.Impact?.ToString() ?? "Unknown",
                Problem = data.ShortDescription?.Problem ?? "",
                Solution = data.ShortDescription?.Solution ?? "",
                ImpactedField = data.ImpactedField ?? "",
                ImpactedValue = data.ImpactedValue ?? "",
                ResourceId = rec.Id.ToString(),
                LastUpdated = data.LastUpdated,
                SubscriptionName = sub.DisplayName,
                SubscriptionId = sub.SubscriptionId,
                TenantId = sub.TenantId
            });
        }

        return results;
    }
}
