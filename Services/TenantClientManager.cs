using System.Collections.Concurrent;
using Azure.Identity;
using Azure.ResourceManager;

namespace FinOps.Services;

public class TenantClientManager
{
    private readonly ArmClient _defaultClient = new(new AzureCliCredential());
    private readonly ConcurrentDictionary<string, ArmClient> _tenantClients = new();

    public ArmClient DefaultClient => _defaultClient;

    public IReadOnlyCollection<string> ConnectedTenantIds => _tenantClients.Keys.ToList().AsReadOnly();

    public ArmClient ConnectTenant(string tenantId)
    {
        return _tenantClients.GetOrAdd(tenantId, tid =>
            new ArmClient(new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tid })));
    }

    public async Task<bool> ValidateTenantAsync(string tenantId)
    {
        try
        {
            var client = ConnectTenant(tenantId);
            await foreach (var _ in client.GetSubscriptions().GetAllAsync())
            {
                return true;
            }
            // No subscriptions found but no error — credential is valid
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ReconnectTenant(string tenantId)
    {
        _tenantClients.TryRemove(tenantId, out _);
        ConnectTenant(tenantId);
    }

    public bool DisconnectTenant(string tenantId)
    {
        return _tenantClients.TryRemove(tenantId, out _);
    }

    public ArmClient GetClientForTenant(string tenantId)
    {
        return _tenantClients.TryGetValue(tenantId, out var client) ? client : _defaultClient;
    }

    public IReadOnlyList<(string? TenantId, ArmClient Client, bool IsDefault)> GetAllClients()
    {
        var clients = new List<(string? TenantId, ArmClient Client, bool IsDefault)>
        {
            (null, _defaultClient, true)
        };

        foreach (var kvp in _tenantClients)
        {
            clients.Add((kvp.Key, kvp.Value, false));
        }

        return clients;
    }
}
