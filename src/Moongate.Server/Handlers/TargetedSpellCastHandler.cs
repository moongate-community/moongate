using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Interfaces.Services.Sessions;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public sealed class TargetedSpellCastHandler : IGameEventListener<TargetedSpellCastEvent>, IMoongateService
{
    private readonly IMagicService _magicService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public TargetedSpellCastHandler(
        IMagicService magicService,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _magicService = magicService;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public Task HandleAsync(TargetedSpellCastEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (!_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session) || session.Character is null)
        {
            return Task.CompletedTask;
        }

        _magicService.TrySetTarget(session.Character.Id, gameEvent.SpellId, gameEvent.TargetSerial);

        return Task.CompletedTask;
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
