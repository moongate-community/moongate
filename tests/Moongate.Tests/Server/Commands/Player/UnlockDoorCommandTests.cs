using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class UnlockDoorCommandTests
{
    private sealed class UnlockDoorCommandTestGameEventBusService : IGameEventBusService
    {
        public TargetRequestCursorEvent? LastTargetRequestEvent { get; private set; }

        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = cancellationToken;

            if (gameEvent is TargetRequestCursorEvent targetRequestCursorEvent)
            {
                LastTargetRequestEvent = targetRequestCursorEvent;
            }

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(Moongate.Server.Interfaces.Services.Events.IGameEventListener<TEvent> listener)
            where TEvent : IGameEvent
            => _ = listener;

        public void TriggerCursorCallback(Serial clickedOnId)
        {
            var packet = new TargetCursorCommandsPacket
            {
                CursorTarget = TargetCursorSelectionType.SelectObject,
                CursorType = TargetCursorType.Neutral,
                ClickedOnId = clickedOnId
            };

            LastTargetRequestEvent!.Value.Callback(new(packet));
        }
    }

    private sealed class UnlockDoorCommandTestDoorLockService : IDoorLockService
    {
        public Serial LastDoorId { get; private set; }

        public Task<DoorLockResult> LockDoorAsync(Serial doorId, CancellationToken cancellationToken = default)
            => Task.FromResult(new DoorLockResult(false, null));

        public Task<bool> UnlockDoorAsync(Serial doorId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            LastDoorId = doorId;

            return Task.FromResult(true);
        }
    }

    private sealed class UnlockDoorCommandTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public int Count => _sessions.Count;
        public void Add(GameSession session) => _sessions[session.SessionId] = session;
        public void Clear() => _sessions.Clear();
        public IReadOnlyCollection<GameSession> GetAll() => _sessions.Values.ToArray();
        public GameSession GetOrCreate(Moongate.Network.Client.MoongateTCPClient client) => throw new NotSupportedException();
        public bool Remove(long sessionId) => _sessions.Remove(sessionId);
        public bool TryGet(long sessionId, out GameSession session) => _sessions.TryGetValue(sessionId, out session!);
        public bool TryGetByCharacterId(Serial characterId, out GameSession session) { session = null!; return false; }
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenDoorSelected_ShouldUnlockDoor()
    {
        var gameEventBus = new UnlockDoorCommandTestGameEventBusService();
        var doorLockService = new UnlockDoorCommandTestDoorLockService();
        var sessionService = new UnlockDoorCommandTestGameNetworkSessionService();
        var session = new GameSession(new(new Moongate.Network.Client.MoongateTCPClient(new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Tcp
        ))));
        sessionService.Add(session);
        var command = new UnlockDoorCommand(gameEventBus, sessionService, doorLockService);
        var context = new CommandSystemContext(".unlock_door", [], CommandSourceType.InGame, session.SessionId, (_, _) => { });

        await command.ExecuteCommandAsync(context);
        gameEventBus.TriggerCursorCallback((Serial)0x40000001u);

        Assert.That(doorLockService.LastDoorId, Is.EqualTo((Serial)0x40000001u));
    }
}
