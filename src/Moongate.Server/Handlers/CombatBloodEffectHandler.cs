using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public sealed class CombatBloodEffectHandler : IGameEventListener<CombatHitEvent>, IMoongateService
{
    private readonly IGameEventBusService _gameEventBusService;

    public CombatBloodEffectHandler(IGameEventBusService gameEventBusService)
    {
        _gameEventBusService = gameEventBusService;
    }

    public async Task HandleAsync(CombatHitEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (gameEvent.Damage <= 0)
        {
            return;
        }

        await _gameEventBusService.PublishAsync(
            new MobilePlayEffectEvent(
                gameEvent.Defender.Id,
                gameEvent.Defender.MapId,
                gameEvent.Defender.Location,
                EffectsUtils.BloodSplash
            ),
            cancellationToken
        );
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
