using FinOps.Models;

namespace FinOps.Services;

public interface ILogAnalyticsService
{
    Task<IReadOnlyList<WorkspaceInfo>> GetWorkspacesAsync(IEnumerable<TenantSubscription> subscriptions);
    Task<WorkspaceUsageData> GetWorkspaceUsageAsync(WorkspaceInfo workspace);
}
