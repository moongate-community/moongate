using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Interaction;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public sealed class QuestJournalRequestedHandler
    : IGameEventListener<QuestJournalRequestedEvent>,
      IMoongateService
{
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IScriptEngineService _scriptEngineService;

    public QuestJournalRequestedHandler(
        IGameNetworkSessionService gameNetworkSessionService,
        IScriptEngineService scriptEngineService
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _scriptEngineService = scriptEngineService;
    }

    public Task HandleAsync(QuestJournalRequestedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (
            !_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session) ||
            session.CharacterId == 0 ||
            session.Character is null ||
            !session.Character.IsPlayer
        )
        {
            return Task.CompletedTask;
        }

        _scriptEngineService.CallFunction(
            "on_quest_journal_requested",
            session.SessionId,
            (uint)session.CharacterId
        );

        return Task.CompletedTask;
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
