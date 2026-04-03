using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Cursors;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.Server.Types.Interaction;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class ResurrectCommandTests
{
    private sealed class ResurrectCommandTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions[session.SessionId] = session;

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

    private sealed class ResurrectCommandTestPlayerTargetService : IPlayerTargetService
    {
        public long LastSessionId { get; private set; }
        public TargetCursorSelectionType LastSelectionType { get; private set; }
        public TargetCursorType LastCursorType { get; private set; }
        public Action<PendingCursorCallback>? Callback { get; private set; }

        public Task SendCancelTargetCursorAsync(long sessionId, Serial cursorId)
        {
            _ = sessionId;
            _ = cursorId;

            return Task.CompletedTask;
        }

        public Task<Serial> SendTargetCursorAsync(
            long sessionId,
            Action<PendingCursorCallback> callback,
            TargetCursorSelectionType selectionType = TargetCursorSelectionType.SelectLocation,
            TargetCursorType cursorType = TargetCursorType.Neutral
        )
        {
            LastSessionId = sessionId;
            LastSelectionType = selectionType;
            LastCursorType = cursorType;
            Callback = callback;

            return Task.FromResult((Serial)0x00001234u);
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class ResurrectCommandTestMobileService : IMobileService
    {
        public Dictionary<Serial, UOMobileEntity> Mobiles { get; } = [];

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
        {
            Mobiles.TryGetValue(id, out var mobile);

            return Task.FromResult(mobile);
        }

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult((false, (UOMobileEntity?)null));
    }

    private sealed class ResurrectCommandTestResurrectionService : IResurrectionService
    {
        public UOMobileEntity? LastPlayer { get; private set; }
        public int CallCount { get; private set; }
        public bool Result { get; set; } = true;

        public Task<bool> TryResurrectAsync(UOMobileEntity player, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            LastPlayer = player;
            CallCount++;

            return Task.FromResult(Result);
        }

        public Task<bool> TryResurrectAsync(
            long sessionId,
            Serial characterId,
            ResurrectionOfferSourceType sourceType,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(false);

        public Task<bool> TryResurrectAsync(
            long sessionId,
            Serial characterId,
            ResurrectionOfferSourceType sourceType,
            Serial sourceSerial,
            int mapId,
            Point3D sourceLocation,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(false);
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenInvoked_ShouldRequestObjectTargetCursor()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var character = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            Name = "Tommy",
            IsPlayer = true
        };
        var session = new GameSession(new(client))
        {
            CharacterId = character.Id,
            Character = character
        };
        var sessionService = new ResurrectCommandTestGameNetworkSessionService();
        sessionService.Add(session);
        var targetService = new ResurrectCommandTestPlayerTargetService();
        var command = new ResurrectCommand(
            sessionService,
            targetService,
            new ResurrectCommandTestMobileService(),
            new ResurrectCommandTestResurrectionService()
        );
        var context = new CommandSystemContext(
            ".resurrect",
            [],
            CommandSourceType.InGame,
            session.SessionId,
            (_, _) => { }
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(targetService.LastSessionId, Is.EqualTo(session.SessionId));
                Assert.That(targetService.LastSelectionType, Is.EqualTo(TargetCursorSelectionType.SelectObject));
                Assert.That(targetService.LastCursorType, Is.EqualTo(TargetCursorType.Helpful));
                Assert.That(targetService.Callback, Is.Not.Null);
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenDeadPlayerTargetSelected_ShouldResurrectTarget()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var caller = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            Name = "Tommy",
            IsPlayer = true
        };
        var target = new UOMobileEntity
        {
            Id = (Serial)0x00000080u,
            Name = "Ghost",
            IsPlayer = true,
            IsAlive = false
        };
        var session = new GameSession(new(client))
        {
            CharacterId = caller.Id,
            Character = caller
        };
        var sessionService = new ResurrectCommandTestGameNetworkSessionService();
        sessionService.Add(session);
        var targetService = new ResurrectCommandTestPlayerTargetService();
        var mobileService = new ResurrectCommandTestMobileService();
        mobileService.Mobiles[target.Id] = target;
        var resurrectionService = new ResurrectCommandTestResurrectionService();
        var output = new List<string>();
        var command = new ResurrectCommand(sessionService, targetService, mobileService, resurrectionService);
        var context = new CommandSystemContext(
            ".resurrect",
            [],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        targetService.Callback!(
            new(
                new()
                {
                    CursorTarget = TargetCursorSelectionType.SelectObject,
                    CursorType = TargetCursorType.Helpful,
                    ClickedOnId = target.Id
                }
            )
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(resurrectionService.CallCount, Is.EqualTo(1));
                Assert.That(resurrectionService.LastPlayer, Is.SameAs(target));
                Assert.That(output[^1], Is.EqualTo("Resurrected Ghost."));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenTargetIsAlive_ShouldPrintError()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var caller = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            Name = "Tommy",
            IsPlayer = true
        };
        var target = new UOMobileEntity
        {
            Id = (Serial)0x00000081u,
            Name = "Alive",
            IsPlayer = true,
            IsAlive = true
        };
        var session = new GameSession(new(client))
        {
            CharacterId = caller.Id,
            Character = caller
        };
        var sessionService = new ResurrectCommandTestGameNetworkSessionService();
        sessionService.Add(session);
        var targetService = new ResurrectCommandTestPlayerTargetService();
        var mobileService = new ResurrectCommandTestMobileService();
        mobileService.Mobiles[target.Id] = target;
        var output = new List<string>();
        var command = new ResurrectCommand(
            sessionService,
            targetService,
            mobileService,
            new ResurrectCommandTestResurrectionService()
        );
        var context = new CommandSystemContext(
            ".resurrect",
            [],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        targetService.Callback!(
            new(
                new()
                {
                    CursorTarget = TargetCursorSelectionType.SelectObject,
                    CursorType = TargetCursorType.Helpful,
                    ClickedOnId = target.Id
                }
            )
        );

        Assert.That(output[^1], Is.EqualTo("Target is not a dead player."));
    }
}
