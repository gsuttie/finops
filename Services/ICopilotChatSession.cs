namespace FinOps.Services;

public interface ICopilotChatSession : IAsyncDisposable
{
    Task SendAsync(string message, Action<string> onDelta, CancellationToken ct = default);
}
