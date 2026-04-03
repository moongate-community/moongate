using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Quests;
using Moongate.Server.Interfaces.Services.Sessions;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public sealed class QuestProgressHandler
    : IGameEventListener<MobileDeathEvent>,
      IGameEventListener<ItemMovedEvent>,
      IMoongateService
{
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IQuestService _questService;

    public QuestProgressHandler(
        IGameNetworkSessionService gameNetworkSessionService,
        IQuestService questService
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _questService = questService;
    }

    public async Task HandleAsync(MobileDeathEvent gameEvent, CancellationToken cancellationToken = default)
    {
        var killer = gameEvent.Killer;

        if (killer is null || !killer.IsPlayer)
        {
            return;
        }

        if (!_gameNetworkSessionService.TryGetByCharacterId(killer.Id, out var session))
        {
            return;
        }

        var player = session.Character;

        if (player is null || !player.IsPlayer)
        {
            return;
        }

        await _questService.OnMobileKilledAsync(player, gameEvent.Victim, cancellationToken);
    }

    public async Task HandleAsync(ItemMovedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (!_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session))
        {
            return;
        }

        var player = session.Character;

        if (player is null || !player.IsPlayer)
        {
            return;
        }

        await _questService.ReevaluateInventoryAsync(player, cancellationToken);
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
