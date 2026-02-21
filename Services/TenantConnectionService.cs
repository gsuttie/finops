using System.Diagnostics;

namespace FinOps.Services;

public class TenantConnectionService(TenantClientManager tenantClientManager) : ITenantConnectionService
{
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(5);

    public async Task<TenantConnectionResult> ConnectWithFallbackAsync(string tenantId, Action<string>? onStatusChanged = null)
    {
        if (!Guid.TryParse(tenantId, out _))
            return new TenantConnectionResult(TenantConnectionStatus.Failed, "Invalid tenant ID format.");

        onStatusChanged?.Invoke("Checking existing credentials...");

        if (await tenantClientManager.ValidateTenantAsync(tenantId))
        {
            return new TenantConnectionResult(TenantConnectionStatus.Connected);
        }

        // Credentials failed — launch az login
        onStatusChanged?.Invoke("Opening browser for Azure login...");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "az",
                Arguments = $"login --tenant {tenantId}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                tenantClientManager.DisconnectTenant(tenantId);
                return new TenantConnectionResult(TenantConnectionStatus.Failed, "Failed to start az login process.");
            }

            using var cts = new CancellationTokenSource(LoginTimeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                tenantClientManager.DisconnectTenant(tenantId);
                return new TenantConnectionResult(TenantConnectionStatus.Failed, "Login timed out after 5 minutes.");
            }

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                tenantClientManager.DisconnectTenant(tenantId);
                return new TenantConnectionResult(TenantConnectionStatus.Failed, $"az login failed: {stderr}");
            }
        }
        catch (Exception ex)
        {
            tenantClientManager.DisconnectTenant(tenantId);
            return new TenantConnectionResult(TenantConnectionStatus.Failed, $"Failed to run az login: {ex.Message}");
        }

        // az login succeeded — reconnect with fresh credentials
        onStatusChanged?.Invoke("Login complete, reconnecting...");
        tenantClientManager.ReconnectTenant(tenantId);

        if (await tenantClientManager.ValidateTenantAsync(tenantId))
        {
            return new TenantConnectionResult(TenantConnectionStatus.ConnectedAfterLogin);
        }

        tenantClientManager.DisconnectTenant(tenantId);
        return new TenantConnectionResult(TenantConnectionStatus.Failed, "Credentials still invalid after login. Check that you have access to this tenant.");
    }
}
