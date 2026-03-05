namespace FinOps.Services;

public interface ICopilotService
{
    Task<ICopilotChatSession> CreateChatSessionAsync(CancellationToken ct = default);
}
