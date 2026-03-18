using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public sealed class CombatHitSoundHandlerTests
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
    public async Task HandleAsync_ShouldPublishAttackerAndDefenderSounds()
    {
        var eventBus = new RecordingGameEventBusService();
        var resolver = new MobileCombatSoundResolver();
        var handler = new CombatHitSoundHandler(resolver, eventBus);
        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x0100,
            MapId = 1,
            Location = new Point3D(100, 100, 0),
            Sounds =
            {
                [MobileSoundType.Attack] = 0x023B
            }
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x0200,
            MapId = 1,
            Location = new Point3D(101, 100, 0),
            Sounds =
            {
                [MobileSoundType.Defend] = 0x0140
            }
        };

        await handler.HandleAsync(new CombatHitEvent(attacker.Id, defender.Id, attacker.MapId, attacker.Location, 6, attacker, defender));

        Assert.Multiple(
            () =>
            {
                Assert.That(eventBus.Events, Has.Count.EqualTo(2));
                Assert.That(((MobilePlaySoundEvent)eventBus.Events[0]).SoundModel, Is.EqualTo((ushort)0x023B));
                Assert.That(((MobilePlaySoundEvent)eventBus.Events[1]).SoundModel, Is.EqualTo((ushort)0x0140));
            }
        );
    }
}
