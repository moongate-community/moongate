using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public sealed class CombatMissSoundHandlerTests
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
        var handler = new CombatMissSoundHandler(resolver, eventBus);
        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x0100,
            MapId = 1,
            Location = new(100, 100, 0),
            Sounds =
            {
                [MobileSoundType.Attack] = 0x023B
            }
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x0200,
            MapId = 1,
            Location = new(101, 100, 0),
            Sounds =
            {
                [MobileSoundType.Defend] = 0x0140
            }
        };

        await handler.HandleAsync(new(attacker.Id, defender.Id, attacker.MapId, attacker.Location, attacker, defender));

        Assert.Multiple(
            () =>
            {
                Assert.That(eventBus.Events, Has.Count.EqualTo(2));
                Assert.That(((MobilePlaySoundEvent)eventBus.Events[0]).SoundModel, Is.EqualTo((ushort)0x023B));
                Assert.That(((MobilePlaySoundEvent)eventBus.Events[1]).SoundModel, Is.EqualTo((ushort)0x0140));
            }
        );
    }

    [Test]
    public async Task HandleAsync_ShouldPreferEquippedWeaponMissSoundForAttacker()
    {
        var eventBus = new RecordingGameEventBusService();
        var resolver = new MobileCombatSoundResolver();
        var handler = new CombatMissSoundHandler(resolver, eventBus);
        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x0100,
            MapId = 1,
            Location = new(100, 100, 0),
            Sounds =
            {
                [MobileSoundType.Attack] = 0x023B
            }
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x0200,
            MapId = 1,
            Location = new(101, 100, 0),
            Sounds =
            {
                [MobileSoundType.Defend] = 0x0140
            }
        };
        attacker.AddEquippedItem(
            ItemLayerType.TwoHanded,
            new UOItemEntity
            {
                Id = (Serial)0x0300,
                ItemId = 0x13B2,
                MissSound = 0x0238,
                WeaponSkill = UOSkillName.Archery
            }
        );

        await handler.HandleAsync(new(attacker.Id, defender.Id, attacker.MapId, attacker.Location, attacker, defender));

        Assert.Multiple(
            () =>
            {
                Assert.That(eventBus.Events, Has.Count.EqualTo(2));
                Assert.That(((MobilePlaySoundEvent)eventBus.Events[0]).SoundModel, Is.EqualTo((ushort)0x0238));
                Assert.That(((MobilePlaySoundEvent)eventBus.Events[1]).SoundModel, Is.EqualTo((ushort)0x0140));
            }
        );
    }
}
