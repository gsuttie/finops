using FinOps.Models;

namespace FinOps.Services;

public interface IPolicyService
{
    Task<IReadOnlyList<PolicyAssignmentInfo>> GetTagInheritancePolicyAssignmentsAsync(
        TenantSubscription subscription);

    Task<PolicyOperationResult> AssignTagInheritancePolicyAsync(
        TenantSubscription subscription, string tagName, string? tagValue = null);

    Task<PolicyOperationResult> RemovePolicyAssignmentAsync(
        TenantSubscription subscription, string assignmentName);
}
