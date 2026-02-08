using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.Authorization.Models;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using FinOps.Models;

namespace FinOps.Services;

public class PolicyService(TenantClientManager tenantClientManager) : IPolicyService
{
    private const string TagInheritancePolicyDefinitionId =
        "/providers/Microsoft.Authorization/policyDefinitions/b27a0cbd-a167-4dfa-ae64-4337be671140";

    // Built-in "Contributor" role definition ID
    private const string ContributorRoleDefinitionId =
        "/providers/Microsoft.Authorization/roleDefinitions/b24988ac-6180-42a0-ab88-20f7382dd24c";

    public async Task<IReadOnlyList<PolicyAssignmentInfo>> GetTagInheritancePolicyAssignmentsAsync(
        TenantSubscription subscription)
    {
        var client = tenantClientManager.GetClientForTenant(subscription.TenantId);
        var subResource = client.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));

        var results = new List<PolicyAssignmentInfo>();

        await foreach (var assignment in subResource.GetPolicyAssignments().GetAllAsync())
        {
            if (!string.Equals(assignment.Data.PolicyDefinitionId,
                    TagInheritancePolicyDefinitionId, StringComparison.OrdinalIgnoreCase))
                continue;

            string? tagName = null;
            string? tagValue = null;
            if (assignment.Data.Parameters.TryGetValue("tagName", out var nameParam))
            {
                tagName = nameParam.Value?.ToString();
            }
            if (assignment.Data.Parameters.TryGetValue("tagValue", out var valueParam))
            {
                tagValue = valueParam.Value?.ToString();
            }

            results.Add(new PolicyAssignmentInfo
            {
                Name = assignment.Data.Name,
                Id = assignment.Data.Id.ToString(),
                DisplayName = assignment.Data.DisplayName,
                Description = assignment.Data.Description,
                PolicyDefinitionId = assignment.Data.PolicyDefinitionId,
                TagName = tagName,
                TagValue = tagValue,
                EnforcementMode = assignment.Data.EnforcementMode?.ToString(),
                HasManagedIdentity = assignment.Data.ManagedIdentity?.ManagedServiceIdentityType != ManagedServiceIdentityType.None
                    && assignment.Data.ManagedIdentity?.ManagedServiceIdentityType != null
            });
        }

        return results;
    }

    public async Task<PolicyOperationResult> AssignTagInheritancePolicyAsync(
        TenantSubscription subscription, string tagName, string? tagValue = null)
    {
        try
        {
            var client = tenantClientManager.GetClientForTenant(subscription.TenantId);
            var subResource = client.GetSubscriptionResource(
                SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));

            var assignmentName = $"inherit-tag-{tagName}";
            var hasValue = !string.IsNullOrWhiteSpace(tagValue);

            var data = new PolicyAssignmentData
            {
                DisplayName = hasValue
                    ? $"Inherit tag '{tagName}={tagValue}' from subscription"
                    : $"Inherit tag '{tagName}' from subscription",
                Description = hasValue
                    ? $"Automatically inherits the '{tagName}' tag (value: '{tagValue}') from the subscription to resources that are missing this tag."
                    : $"Automatically inherits the '{tagName}' tag from the subscription to resources that are missing this tag.",
                PolicyDefinitionId = new ResourceIdentifier(TagInheritancePolicyDefinitionId),
                EnforcementMode = EnforcementMode.Default,
                ManagedIdentity = new ManagedServiceIdentity(ManagedServiceIdentityType.SystemAssigned),
                Location = new Azure.Core.AzureLocation("uksouth")
            };

            data.Parameters["tagName"] = new ArmPolicyParameterValue { Value = BinaryData.FromObjectAsJson(tagName) };

            var response = await subResource.GetPolicyAssignments().CreateOrUpdateAsync(
                WaitUntil.Completed, assignmentName, data);

            // Grant the managed identity Contributor role so it can modify tags
            var principalId = response.Value.Data.ManagedIdentity?.PrincipalId;
            if (principalId.HasValue)
            {
                var roleAssignmentId = Guid.NewGuid().ToString();
                var roleData = new RoleAssignmentCreateOrUpdateContent(
                    new ResourceIdentifier(ContributorRoleDefinitionId),
                    principalId.Value)
                {
                    PrincipalType = RoleManagementPrincipalType.ServicePrincipal
                };

                var scope = SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId);
                try
                {
                    await client.GetRoleAssignments(scope).CreateOrUpdateAsync(
                        WaitUntil.Completed, roleAssignmentId, roleData);
                }
                catch (RequestFailedException ex) when (ex.Status == 409)
                {
                    // Role assignment already exists — safe to ignore
                }
            }

            return new PolicyOperationResult
            {
                SubscriptionName = subscription.DisplayName,
                SubscriptionId = subscription.SubscriptionId,
                Operation = "Assign",
                Success = true,
                PolicyAssignmentName = assignmentName
            };
        }
        catch (RequestFailedException ex)
        {
            return new PolicyOperationResult
            {
                SubscriptionName = subscription.DisplayName,
                SubscriptionId = subscription.SubscriptionId,
                Operation = "Assign",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<PolicyOperationResult> RemovePolicyAssignmentAsync(
        TenantSubscription subscription, string assignmentName)
    {
        try
        {
            var client = tenantClientManager.GetClientForTenant(subscription.TenantId);
            var subResource = client.GetSubscriptionResource(
                SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));

            var assignment = await subResource.GetPolicyAssignments().GetAsync(assignmentName);
            await assignment.Value.DeleteAsync(WaitUntil.Completed);

            return new PolicyOperationResult
            {
                SubscriptionName = subscription.DisplayName,
                SubscriptionId = subscription.SubscriptionId,
                Operation = "Remove",
                Success = true,
                PolicyAssignmentName = assignmentName
            };
        }
        catch (RequestFailedException ex)
        {
            return new PolicyOperationResult
            {
                SubscriptionName = subscription.DisplayName,
                SubscriptionId = subscription.SubscriptionId,
                Operation = "Remove",
                Success = false,
                ErrorMessage = ex.Message,
                PolicyAssignmentName = assignmentName
            };
        }
    }
}
