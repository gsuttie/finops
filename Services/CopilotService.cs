namespace FinOps.Services;

public class CopilotService : ICopilotService
{
    public async Task<ICopilotChatSession> CreateChatSessionAsync(CancellationToken ct = default)
        => await CopilotChatSession.CreateAsync(ct);
}
