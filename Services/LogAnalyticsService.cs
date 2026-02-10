using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.OperationalInsights;
using Azure.ResourceManager.Resources;
using FinOps.Models;

namespace FinOps.Services;

public class LogAnalyticsService(TenantClientManager tenantClientManager) : ILogAnalyticsService
{
    public async Task<IReadOnlyList<WorkspaceInfo>> GetWorkspacesAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subList = subscriptions.ToList();
        if (subList.Count == 0)
            return [];

        var tenantGroups = subList.GroupBy(s => s.TenantId);

        var tenantTasks = tenantGroups.Select(group =>
            GetWorkspacesForTenantAsync(group.Key, group.ToList()));
        var tenantResults = await Task.WhenAll(tenantTasks);

        return tenantResults.SelectMany(r => r).ToList();
    }

    public async Task<WorkspaceUsageData> GetWorkspaceUsageAsync(WorkspaceInfo workspace)
    {
        var credential = new AzureCliCredential(
            new AzureCliCredentialOptions { TenantId = workspace.TenantId });
        var logsClient = new LogsQueryClient(credential);

        const string dailyQuery = """
            Usage
            | where IsBillable == true
            | where TimeGenerated > ago(30d)
            | summarize DataGB = sum(Quantity) / 1000 by bin(TimeGenerated, 1d)
            | order by TimeGenerated asc
            """;

        const string byTypeQuery = """
            Usage
            | where IsBillable == true
            | where TimeGenerated > ago(30d)
            | summarize DataGB = sum(Quantity) / 1000 by DataType
            | order by DataGB desc
            """;

        var dailyTask = logsClient.QueryWorkspaceAsync(
            workspace.CustomerId, dailyQuery, QueryTimeRange.All);
        var byTypeTask = logsClient.QueryWorkspaceAsync(
            workspace.CustomerId, byTypeQuery, QueryTimeRange.All);

        await Task.WhenAll(dailyTask, byTypeTask);

        var dailyResult = dailyTask.Result.Value;
        var byTypeResult = byTypeTask.Result.Value;

        var dailyIngestion = dailyResult.Table.Rows
            .Select(row => new DailyUsage
            {
                Date = row.GetDateTimeOffset("TimeGenerated") ?? DateTimeOffset.MinValue,
                DataGb = row.GetDouble("DataGB") ?? 0
            })
            .ToList();

        var usageByType = byTypeResult.Table.Rows
            .Select(row => new DataTypeUsage
            {
                DataType = row.GetString("DataType") ?? "Unknown",
                DataGb = row.GetDouble("DataGB") ?? 0
            })
            .ToList();

        return new WorkspaceUsageData
        {
            WorkspaceName = workspace.Name,
            CustomerId = workspace.CustomerId,
            DailyIngestion = dailyIngestion,
            UsageByType = usageByType
        };
    }

    private async Task<List<WorkspaceInfo>> GetWorkspacesForTenantAsync(
        string tenantId, List<TenantSubscription> subscriptions)
    {
        var client = tenantClientManager.GetClientForTenant(tenantId);

        var subTasks = subscriptions.Select(sub =>
            GetWorkspacesForSubscriptionAsync(client, sub));
        var subResults = await Task.WhenAll(subTasks);

        return subResults.SelectMany(r => r).ToList();
    }

    private static Task<List<WorkspaceInfo>> GetWorkspacesForSubscriptionAsync(
        ArmClient client, TenantSubscription sub)
    {
        var results = new List<WorkspaceInfo>();
        var subResource = client.GetSubscriptionResource(
            new Azure.Core.ResourceIdentifier($"/subscriptions/{sub.SubscriptionId}"));

        foreach (var workspace in subResource.GetOperationalInsightsWorkspaces())
        {
            var data = workspace.Data;
            results.Add(new WorkspaceInfo
            {
                WorkspaceResourceId = workspace.Id.ToString(),
                Name = data.Name,
                CustomerId = data.CustomerId?.ToString() ?? "",
                ResourceGroup = workspace.Id.ResourceGroupName ?? "",
                Location = data.Location.ToString(),
                SkuName = data.Sku?.Name.ToString() ?? "Unknown",
                RetentionInDays = data.RetentionInDays ?? 30,
                DailyQuotaGb = data.WorkspaceCapping?.DailyQuotaInGB,
                SubscriptionName = sub.DisplayName,
                SubscriptionId = sub.SubscriptionId,
                TenantId = sub.TenantId
            });
        }

        return Task.FromResult(results);
    }
}
