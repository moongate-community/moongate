using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Magic;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Handlers;

public sealed class SpellCastRequestedHandlerTests
{
    private sealed class RecordingMagicService : IMagicService
    {
        public UOMobileEntity? LastCaster { get; private set; }

        public int? LastSpellId { get; private set; }

        public int CallCount { get; private set; }

        public bool IsCasting(Serial casterId)
        {
            _ = casterId;

            return false;
        }

        public bool TrySetTarget(Serial casterId, int spellId, Serial targetId)
        {
            _ = casterId;
            _ = spellId;
            _ = targetId;

            return false;
        }

        public ValueTask<bool> TrySetTargetAsync(
            Serial casterId,
            int spellId,
            SpellTargetData target,
            CancellationToken cancellationToken = default
        )
        {
            _ = target;
            _ = cancellationToken;

            return ValueTask.FromResult(TrySetTarget(casterId, spellId, target.TargetId));
        }

        public ValueTask<bool> TryCastAsync(
            UOMobileEntity caster,
            int spellId,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            LastCaster = caster;
            LastSpellId = spellId;
            CallCount++;

            return ValueTask.FromResult(true);
        }

        public void Interrupt(Serial casterId)
            => _ = casterId;

        public ValueTask OnCastTimerExpiredAsync(Serial casterId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _ = casterId;

            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task HandleAsync_WhenSessionHasCharacter_ShouldCallMagicService()
    {
        var magicService = new RecordingMagicService();
        var sessionService = new FakeGameNetworkSessionService();
        var handler = new SpellCastRequestedHandler(magicService, sessionService);
        var character = new UOMobileEntity
        {
            Id = (Serial)0x00000001u
        };

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            Character = character,
            CharacterId = character.Id
        };

        sessionService.Add(session);

        await handler.HandleAsync(new(session.SessionId, 45));

        Assert.Multiple(
            () =>
            {
                Assert.That(magicService.CallCount, Is.EqualTo(1));
                Assert.That(magicService.LastCaster, Is.SameAs(character));
                Assert.That(magicService.LastSpellId, Is.EqualTo(45));
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenSessionMissing_ShouldNotCallMagicService()
    {
        var magicService = new RecordingMagicService();
        var sessionService = new FakeGameNetworkSessionService();
        var handler = new SpellCastRequestedHandler(magicService, sessionService);

        await handler.HandleAsync(new(99, 45));

        Assert.That(magicService.CallCount, Is.Zero);
    }
}
