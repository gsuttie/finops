using GitHub.Copilot.SDK;

namespace FinOps.Services;

public sealed class CopilotChatSession : ICopilotChatSession
{
    private const string SystemPrompt =
        "You are an expert Azure FinOps and cloud cost optimization consultant. " +
        "Answer questions about Azure costs, budgets, rightsizing, tagging strategies, " +
        "cost management best practices, and Azure pricing clearly and concisely.";

    private readonly CopilotClient _client;
    private readonly CopilotSession _session;

    private CopilotChatSession(CopilotClient client, CopilotSession session)
    {
        _client = client;
        _session = session;
    }

    public static async Task<CopilotChatSession> CreateAsync(CancellationToken ct = default)
    {
        var client = new CopilotClient();
        await client.StartAsync();

        var session = await client.CreateSessionAsync(new SessionConfig
        {
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = SystemPrompt
            },
            Streaming = true,
            OnPermissionRequest = PermissionHandler.ApproveAll
        }, ct);

        return new CopilotChatSession(client, session);
    }

    public async Task SendAsync(string message, Action<string> onDelta, CancellationToken ct = default)
    {
        var done = new TaskCompletionSource();

        using var _ = _session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta when !string.IsNullOrEmpty(delta.Data.DeltaContent):
                    onDelta(delta.Data.DeltaContent);
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case SessionErrorEvent err:
                    done.TrySetException(new InvalidOperationException(err.Data.Message));
                    break;
            }
        });

        await _session.SendAsync(new MessageOptions { Prompt = message }, ct);
        await done.Task.WaitAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _session.DisposeAsync();
        await _client.DisposeAsync();
    }
}
