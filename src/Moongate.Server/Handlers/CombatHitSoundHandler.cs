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
public sealed class CombatHitSoundHandler : IGameEventListener<CombatHitEvent>, IMoongateService
{
    private readonly MobileCombatSoundResolver _resolver;
    private readonly IGameEventBusService _gameEventBusService;

    public CombatHitSoundHandler(
        MobileCombatSoundResolver resolver,
        IGameEventBusService gameEventBusService
    )
    {
        _resolver = resolver;
        _gameEventBusService = gameEventBusService;
    }

    public async Task HandleAsync(CombatHitEvent gameEvent, CancellationToken cancellationToken = default)
    {
        await PublishIfResolvedAsync(gameEvent.Attacker, MobileSoundType.Attack, cancellationToken);
        await PublishIfResolvedAsync(gameEvent.Defender, MobileSoundType.Defend, cancellationToken);
    }

    public Task StartAsync() => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;

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
