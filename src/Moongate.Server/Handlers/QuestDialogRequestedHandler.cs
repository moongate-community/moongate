using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Interaction;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public sealed class QuestDialogRequestedHandler
    : IGameEventListener<QuestDialogRequestedEvent>,
      IMoongateService
{
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IScriptEngineService _scriptEngineService;

    public QuestDialogRequestedHandler(
        IGameNetworkSessionService gameNetworkSessionService,
        IScriptEngineService scriptEngineService
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _scriptEngineService = scriptEngineService;
    }

    public Task HandleAsync(QuestDialogRequestedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (
            !_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session) ||
            session.CharacterId == 0 ||
            session.Character is null
        )
        {
            return Task.CompletedTask;
        }

        _scriptEngineService.CallFunction(
            "on_quest_dialog_requested",
            session.SessionId,
            (uint)session.CharacterId,
            (uint)gameEvent.TargetSerial
        );

        return Task.CompletedTask;
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
