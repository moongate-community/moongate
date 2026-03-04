using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.GeneralInformation;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog.Events;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class TeleportCommandTests
{
    [Test]
    public async Task ExecuteCommandAsync_WhenArgumentsAreInvalid_ShouldPrintUsageAndSkipActions()
    {
        var sessionService = new TeleportTestGameNetworkSessionService();
        var gameEventBusService = new TeleportTestGameEventBusService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var command = new TeleportCommand(sessionService, gameEventBusService, outgoingPacketQueue);
        var output = new List<string>();
        var context = new CommandSystemContext(
            "teleport 1 100",
            ["1", "100"],
            CommandSourceType.InGame,
            1,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(output, Has.Count.EqualTo(1));
                Assert.That(output[0], Is.EqualTo("Usage: .teleport <mapId> <x> <y> <z>"));
                Assert.That(gameEventBusService.PublishedEvents, Is.Empty);
                Assert.That(outgoingPacketQueue.CurrentQueueDepth, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenSessionExists_ShouldTeleportAndPublishMovementEvent()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var character = new UOMobileEntity
        {
            Id = (Serial)0x00000002,
            MapId = 1,
            Location = new Point3D(100, 200, 0)
        };
        var session = new GameSession(new GameNetworkSession(client))
        {
            CharacterId = character.Id,
            Character = character
        };
        var sessionService = new TeleportTestGameNetworkSessionService(session);
        var gameEventBusService = new TeleportTestGameEventBusService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var command = new TeleportCommand(sessionService, gameEventBusService, outgoingPacketQueue);
        var output = new List<string>();
        var context = new CommandSystemContext(
            "teleport 1 3613 2585 0",
            ["1", "3613", "2585", "0"],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(character.MapId, Is.EqualTo(1));
                Assert.That(character.Location, Is.EqualTo(new Point3D(3613, 2585, 0)));
                Assert.That(gameEventBusService.PublishedEvents, Has.Count.EqualTo(1));
                Assert.That(gameEventBusService.PublishedEvents[0], Is.TypeOf<MobilePositionChangedEvent>());
                Assert.That(output[^1], Is.EqualTo("Teleported to map 1 at (3613, 2585, 0)."));
            }
        );

        Assert.That(outgoingPacketQueue.TryDequeue(out var first), Is.True);
        Assert.That(first.Packet, Is.TypeOf<GeneralInformationPacket>());
        Assert.That(outgoingPacketQueue.TryDequeue(out var second), Is.True);
        Assert.That(second.Packet, Is.TypeOf<DrawPlayerPacket>());
    }

    private sealed class TeleportTestGameEventBusService : IGameEventBusService
    {
        public List<IGameEvent> PublishedEvents { get; } = [];

        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = cancellationToken;
            PublishedEvents.Add(gameEvent);

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent
            => _ = listener;
    }

    private sealed class TeleportTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public TeleportTestGameNetworkSessionService() { }

        public TeleportTestGameNetworkSessionService(GameSession session)
        {
            _sessions[session.SessionId] = session;
        }

        public int Count => _sessions.Count;

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => _sessions.Values.ToArray();

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
            => _sessions.Remove(sessionId);

        public bool TryGet(long sessionId, out GameSession session)
            => _sessions.TryGetValue(sessionId, out session!);

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = _sessions.Values.FirstOrDefault(current => current.CharacterId == characterId)!;
            return session is not null;
        }
    }
}
