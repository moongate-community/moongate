using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public sealed class CombatStartedSoundHandler : IGameEventListener<CombatStartedEvent>, IMoongateService
{
    private readonly MobileCombatSoundResolver _resolver;
    private readonly IGameEventBusService _gameEventBusService;

    public CombatStartedSoundHandler(
        MobileCombatSoundResolver resolver,
        IGameEventBusService gameEventBusService
    )
    {
        _resolver = resolver;
        _gameEventBusService = gameEventBusService;
    }

    public async Task HandleAsync(CombatStartedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (!_resolver.TryResolve(gameEvent.Attacker, MobileSoundType.StartAttack, out var soundId))
        {
            return;
        }

        await _gameEventBusService.PublishAsync(
            new MobilePlaySoundEvent(gameEvent.Attacker.Id, gameEvent.MapId, gameEvent.Location, (ushort)soundId),
            cancellationToken
        );
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
