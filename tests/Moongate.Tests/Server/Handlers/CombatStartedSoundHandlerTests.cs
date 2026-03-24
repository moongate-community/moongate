using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public sealed class CombatStartedSoundHandlerTests
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
    public async Task HandleAsync_ShouldPublishMobilePlaySoundEventForAttacker()
    {
        var eventBus = new RecordingGameEventBusService();
        var resolver = new MobileCombatSoundResolver();
        var handler = new CombatStartedSoundHandler(resolver, eventBus);
        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x0100,
            MapId = 1,
            Location = new(100, 100, 0),
            Sounds =
            {
                [MobileSoundType.StartAttack] = 0x0135
            }
        };

        await handler.HandleAsync(new(attacker.Id, (Serial)0x0200, attacker.MapId, attacker.Location, attacker));

        Assert.That(eventBus.Events, Has.Count.EqualTo(1));
        Assert.That(eventBus.Events[0], Is.TypeOf<MobilePlaySoundEvent>());
        Assert.That(((MobilePlaySoundEvent)eventBus.Events[0]).SoundModel, Is.EqualTo((ushort)0x0135));
    }
}
