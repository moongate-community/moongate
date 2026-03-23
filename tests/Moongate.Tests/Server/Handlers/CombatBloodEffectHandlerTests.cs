using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Handlers;

public sealed class CombatBloodEffectHandlerTests
{
    private sealed class RecordingGameEventBusService : IGameEventBusService
    {
        public List<object> Events { get; } = [];

        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = cancellationToken;
            Events.Add(gameEvent!);

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent
            => _ = listener;
    }

    [Test]
    public async Task HandleAsync_ShouldPublishBloodSplashForDefenderLocation()
    {
        var eventBus = new RecordingGameEventBusService();
        var handler = new CombatBloodEffectHandler(eventBus);
        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x0100,
            MapId = 1,
            Location = new Point3D(100, 100, 0)
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x0200,
            MapId = 1,
            Location = new Point3D(101, 100, 0)
        };

        await handler.HandleAsync(new CombatHitEvent(attacker.Id, defender.Id, 1, attacker.Location, 6, attacker, defender));

        Assert.That(eventBus.Events, Has.Count.EqualTo(1));
        Assert.That(eventBus.Events[0], Is.TypeOf<MobilePlayEffectEvent>());

        var effectEvent = (MobilePlayEffectEvent)eventBus.Events[0];

        Assert.Multiple(
            () =>
            {
                Assert.That(effectEvent.MobileId, Is.EqualTo(defender.Id));
                Assert.That(effectEvent.MapId, Is.EqualTo(defender.MapId));
                Assert.That(effectEvent.Location, Is.EqualTo(defender.Location));
                Assert.That(effectEvent.ItemId, Is.EqualTo(EffectsUtils.BloodSplash));
            }
        );
    }
}
