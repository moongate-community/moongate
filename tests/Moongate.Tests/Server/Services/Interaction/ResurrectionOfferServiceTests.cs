using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Services.Interaction;
using Moongate.Server.Types.Interaction;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class ResurrectionOfferServiceTests
{
    [Test]
    public async Task TryCreateOfferAsync_WhenSessionMatchesCharacter_ShouldOpenLuaUiAndAcceptPendingOffer()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00001234u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00001234u,
                IsPlayer = true,
                IsAlive = false,
                MapId = 0,
                Location = new Point3D(100, 100, 0)
            }
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var resurrectionService = new TestResurrectionService();
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var timeProvider = new ManualTimeProvider();
        var service = new ResurrectionOfferService(sessionService, resurrectionService, scriptEngine, timeProvider);

        var created = await service.TryCreateOfferAsync(
            session.SessionId,
            session.CharacterId,
            ResurrectionOfferSourceType.Healer
        );
        var accepted = await service.TryAcceptAsync(session.SessionId);

        Assert.Multiple(
            () =>
            {
                Assert.That(created, Is.True);
                Assert.That(accepted, Is.True);
                Assert.That(scriptEngine.LastCallbackName, Is.EqualTo("on_resurrection_offer"));
                Assert.That(scriptEngine.LastCallbackArgs, Has.Length.EqualTo(3));
                Assert.That(scriptEngine.LastCallbackArgs![0], Is.EqualTo(session.SessionId));
                Assert.That(scriptEngine.LastCallbackArgs[1], Is.EqualTo((uint)session.CharacterId));
                Assert.That(scriptEngine.LastCallbackArgs[2], Is.EqualTo("healer"));
                Assert.That(resurrectionService.CallCount, Is.EqualTo(1));
                Assert.That(resurrectionService.LastSessionId, Is.EqualTo(session.SessionId));
                Assert.That(resurrectionService.LastCharacterId, Is.EqualTo(session.CharacterId));
                Assert.That(resurrectionService.LastSourceType, Is.EqualTo(ResurrectionOfferSourceType.Healer));
                Assert.That(resurrectionService.LastSourceSerial, Is.EqualTo(session.Character.Id));
                Assert.That(resurrectionService.LastMapId, Is.EqualTo(session.Character.MapId));
                Assert.That(resurrectionService.LastSourceLocation, Is.EqualTo(session.Character.Location));
            }
        );
    }

    [Test]
    public async Task Decline_WhenPendingOfferExists_ShouldRemoveIt()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00005678u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00005678u,
                IsPlayer = true,
                IsAlive = false,
                MapId = 0,
                Location = new Point3D(120, 120, 0)
            }
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var resurrectionService = new TestResurrectionService();
        var service = new ResurrectionOfferService(
            sessionService,
            resurrectionService,
            new GameEventScriptBridgeTestScriptEngineService(),
            new ManualTimeProvider()
        );

        await service.TryCreateOfferAsync(session.SessionId, session.CharacterId, ResurrectionOfferSourceType.Ankh);
        service.Decline(session.SessionId);

        var accepted = await service.TryAcceptAsync(session.SessionId);

        Assert.Multiple(
            () =>
            {
                Assert.That(accepted, Is.False);
                Assert.That(resurrectionService.CallCount, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task TryAcceptAsync_WhenOfferExpires_ShouldReturnFalse()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00009ABCu,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00009ABCu,
                IsPlayer = true,
                IsAlive = false,
                MapId = 0,
                Location = new Point3D(140, 140, 0)
            }
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var resurrectionService = new TestResurrectionService();
        var timeProvider = new ManualTimeProvider();
        var service = new ResurrectionOfferService(
            sessionService,
            resurrectionService,
            new GameEventScriptBridgeTestScriptEngineService(),
            timeProvider
        );

        await service.TryCreateOfferAsync(session.SessionId, session.CharacterId, ResurrectionOfferSourceType.Healer);
        timeProvider.Advance(TimeSpan.FromSeconds(31));

        var accepted = await service.TryAcceptAsync(session.SessionId);

        Assert.Multiple(
            () =>
            {
                Assert.That(accepted, Is.False);
                Assert.That(resurrectionService.CallCount, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task TryCreateOfferAsync_WhenSessionCharacterDoesNotMatch_ShouldReturnFalse()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00001234u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00001234u,
                IsPlayer = true,
                IsAlive = false,
                MapId = 0,
                Location = new Point3D(160, 160, 0)
            }
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var service = new ResurrectionOfferService(
            sessionService,
            new TestResurrectionService(),
            scriptEngine,
            new ManualTimeProvider()
        );

        var created = await service.TryCreateOfferAsync(
            session.SessionId,
            (Serial)0x00004321u,
            ResurrectionOfferSourceType.Healer
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(created, Is.False);
                Assert.That(scriptEngine.LastCallbackName, Is.Null);
                Assert.That(scriptEngine.LastCallbackArgs, Is.Null);
            }
        );
    }

    private sealed class TestResurrectionService : IResurrectionService
    {
        public int CallCount { get; private set; }
        public long LastSessionId { get; private set; }
        public Serial LastCharacterId { get; private set; }
        public ResurrectionOfferSourceType LastSourceType { get; private set; }
        public Serial LastSourceSerial { get; private set; }
        public int LastMapId { get; private set; }
        public Point3D LastSourceLocation { get; private set; }
        public bool Result { get; set; } = true;

        public Task<bool> TryResurrectAsync(
            long sessionId,
            Serial characterId,
            ResurrectionOfferSourceType sourceType,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            CallCount++;
            LastSessionId = sessionId;
            LastCharacterId = characterId;
            LastSourceType = sourceType;

            return Task.FromResult(Result);
        }

        public Task<bool> TryResurrectAsync(UOMobileEntity player, CancellationToken cancellationToken = default)
        {
            _ = player;
            _ = cancellationToken;
            CallCount++;

            return Task.FromResult(Result);
        }

        public Task<bool> TryResurrectAsync(
            long sessionId,
            Serial characterId,
            ResurrectionOfferSourceType sourceType,
            Serial sourceSerial,
            int mapId,
            Point3D sourceLocation,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            CallCount++;
            LastSessionId = sessionId;
            LastCharacterId = characterId;
            LastSourceType = sourceType;
            LastSourceSerial = sourceSerial;
            LastMapId = mapId;
            LastSourceLocation = sourceLocation;

            return Task.FromResult(Result);
        }
    }

    private sealed class TestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly List<GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions.Add(session);

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => _sessions.ToArray();

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
        {
            var removed = _sessions.RemoveAll(session => session.SessionId == sessionId);

            return removed > 0;
        }

        public bool TryGet(long sessionId, out GameSession session)
        {
            session = _sessions.FirstOrDefault(candidate => candidate.SessionId == sessionId)!;

            return session is not null;
        }

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = _sessions.FirstOrDefault(candidate => candidate.CharacterId == characterId)!;

            return session is not null;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
            => _utcNow;

        public void Advance(TimeSpan duration)
            => _utcNow = _utcNow.Add(duration);
    }
}
