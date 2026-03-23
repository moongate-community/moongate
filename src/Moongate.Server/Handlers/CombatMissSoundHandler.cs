using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public sealed class CombatMissSoundHandler : IGameEventListener<CombatMissEvent>, IMoongateService
{
    private readonly MobileCombatSoundResolver _resolver;
    private readonly IGameEventBusService _gameEventBusService;

    public CombatMissSoundHandler(
        MobileCombatSoundResolver resolver,
        IGameEventBusService gameEventBusService
    )
    {
        _resolver = resolver;
        _gameEventBusService = gameEventBusService;
    }

    public async Task HandleAsync(CombatMissEvent gameEvent, CancellationToken cancellationToken = default)
    {
        await PublishAttackerSoundAsync(gameEvent.Attacker, cancellationToken);
        await PublishIfResolvedAsync(gameEvent.Defender, MobileSoundType.Defend, cancellationToken);
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;

    private async Task PublishAttackerSoundAsync(UOMobileEntity attacker, CancellationToken cancellationToken)
    {
        if (!_resolver.TryResolveMissSound(attacker, out var soundId))
        {
            return;
        }

        await _gameEventBusService.PublishAsync(
            new MobilePlaySoundEvent(attacker.Id, attacker.MapId, attacker.Location, (ushort)soundId),
            cancellationToken
        );
    }

    private async Task PublishIfResolvedAsync(
        UOMobileEntity mobile,
        MobileSoundType soundType,
        CancellationToken cancellationToken
    )
    {
        if (!_resolver.TryResolve(mobile, soundType, out var soundId))
        {
            return;
        }

        await _gameEventBusService.PublishAsync(
            new MobilePlaySoundEvent(mobile.Id, mobile.MapId, mobile.Location, (ushort)soundId),
            cancellationToken
        );
    }
}
