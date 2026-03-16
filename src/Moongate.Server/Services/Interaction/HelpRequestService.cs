using Moongate.Scripting.Interfaces;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;

namespace Moongate.Server.Services.Interaction;

public sealed class HelpRequestService : IHelpRequestService
{
    private readonly IScriptEngineService _scriptEngineService;

    public HelpRequestService(IScriptEngineService scriptEngineService)
    {
        _scriptEngineService = scriptEngineService;
    }

    public Task OpenAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (session.CharacterId == 0)
        {
            return Task.CompletedTask;
        }

        _scriptEngineService.CallFunction("on_help_request", session.SessionId, (uint)session.CharacterId);

        return Task.CompletedTask;
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
