using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Handlers;

[TestFixture]
public sealed class TargetedSpellCastHandlerTests
{
    private sealed class RecordingMagicService : IMagicService
    {
        public Serial? LastCasterId { get; private set; }

        public int? LastSpellId { get; private set; }

        public Serial? LastTargetId { get; private set; }

        public int TrySetTargetCalls { get; private set; }

        public bool IsCasting(Serial casterId)
        {
            _ = casterId;

            return false;
        }

        public bool TrySetTarget(Serial casterId, int spellId, Serial targetId)
        {
            LastCasterId = casterId;
            LastSpellId = spellId;
            LastTargetId = targetId;
            TrySetTargetCalls++;

            return true;
        }

        public ValueTask<bool> TryCastAsync(
            UOMobileEntity caster,
            int spellId,
            CancellationToken cancellationToken = default
        )
        {
            _ = caster;
            _ = spellId;
            _ = cancellationToken;

            return ValueTask.FromResult(true);
        }

        public void Interrupt(Serial casterId)
            => _ = casterId;

        public ValueTask OnCastTimerExpiredAsync(Serial casterId, CancellationToken cancellationToken = default)
        {
            _ = casterId;
            _ = cancellationToken;

            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task HandleAsync_WhenSessionHasCharacter_ShouldBindSpellTarget()
    {
        var magicService = new RecordingMagicService();
        var sessionService = new FakeGameNetworkSessionService();
        var handler = new TargetedSpellCastHandler(magicService, sessionService);
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

        await handler.HandleAsync(new(session.SessionId, 45, (Serial)0x00000002u));

        Assert.Multiple(
            () =>
            {
                Assert.That(magicService.TrySetTargetCalls, Is.EqualTo(1));
                Assert.That(magicService.LastCasterId, Is.EqualTo(character.Id));
                Assert.That(magicService.LastSpellId, Is.EqualTo(45));
                Assert.That(magicService.LastTargetId, Is.EqualTo((Serial)0x00000002u));
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenSessionMissing_ShouldNotBindSpellTarget()
    {
        var magicService = new RecordingMagicService();
        var sessionService = new FakeGameNetworkSessionService();
        var handler = new TargetedSpellCastHandler(magicService, sessionService);

        await handler.HandleAsync(new(99, 45, (Serial)0x00000002u));

        Assert.That(magicService.TrySetTargetCalls, Is.Zero);
    }
}
