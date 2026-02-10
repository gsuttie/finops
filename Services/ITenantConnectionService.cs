namespace FinOps.Services;

public enum TenantConnectionStatus
{
    Connected,
    ConnectedAfterLogin,
    Cancelled,
    Failed
}

public record TenantConnectionResult(TenantConnectionStatus Status, string? ErrorMessage = null);

public interface ITenantConnectionService
{
    Task<TenantConnectionResult> ConnectWithFallbackAsync(string tenantId, Action<string>? onStatusChanged = null);
}
