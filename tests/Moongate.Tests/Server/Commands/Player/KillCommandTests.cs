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
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class KillCommandTests
{
    private sealed class KillCommandTestGameNetworkSessionService : IGameNetworkSessionService
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

    private sealed class KillCommandTestPlayerTargetService : IPlayerTargetService
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

    private sealed class KillCommandTestMobileService : IMobileService
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

    private sealed class KillCommandTestDeathService : IDeathService
    {
        public UOMobileEntity? LastVictim { get; private set; }
        public UOMobileEntity? LastKiller { get; private set; }
        public int ForceDeathCalls { get; private set; }

        public Task<bool> ForceDeathAsync(
            UOMobileEntity victim,
            UOMobileEntity? killer,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            LastVictim = victim;
            LastKiller = killer;
            ForceDeathCalls++;

            return Task.FromResult(true);
        }

        public Task<bool> HandleDeathAsync(
            UOMobileEntity victim,
            UOMobileEntity? killer,
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
        var sessionService = new KillCommandTestGameNetworkSessionService();
        sessionService.Add(session);
        var targetService = new KillCommandTestPlayerTargetService();
        var command = new KillCommand(
            sessionService,
            targetService,
            new KillCommandTestMobileService(),
            new KillCommandTestDeathService()
        );
        var context = new CommandSystemContext(
            ".kill",
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
                Assert.That(targetService.LastCursorType, Is.EqualTo(TargetCursorType.Harmful));
                Assert.That(targetService.Callback, Is.Not.Null);
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenMobileTargetSelected_ShouldForceDeathUsingCallerAsKiller()
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
            Name = "Zombie",
            IsAlive = true
        };
        var session = new GameSession(new(client))
        {
            CharacterId = caller.Id,
            Character = caller
        };
        var sessionService = new KillCommandTestGameNetworkSessionService();
        sessionService.Add(session);
        var targetService = new KillCommandTestPlayerTargetService();
        var mobileService = new KillCommandTestMobileService();
        mobileService.Mobiles[target.Id] = target;
        var deathService = new KillCommandTestDeathService();
        var output = new List<string>();
        var command = new KillCommand(sessionService, targetService, mobileService, deathService);
        var context = new CommandSystemContext(
            ".kill",
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
                    CursorType = TargetCursorType.Harmful,
                    ClickedOnId = target.Id
                }
            )
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(deathService.ForceDeathCalls, Is.EqualTo(1));
                Assert.That(deathService.LastVictim, Is.SameAs(target));
                Assert.That(deathService.LastKiller, Is.SameAs(caller));
                Assert.That(output[^1], Is.EqualTo("Killed Zombie."));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenTargetIsNotMobile_ShouldPrintError()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var caller = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            Name = "Tommy",
            IsPlayer = true
        };
        var session = new GameSession(new(client))
        {
            CharacterId = caller.Id,
            Character = caller
        };
        var sessionService = new KillCommandTestGameNetworkSessionService();
        sessionService.Add(session);
        var targetService = new KillCommandTestPlayerTargetService();
        var deathService = new KillCommandTestDeathService();
        var output = new List<string>();
        var command = new KillCommand(
            sessionService,
            targetService,
            new KillCommandTestMobileService(),
            deathService
        );
        var context = new CommandSystemContext(
            ".kill",
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
                    CursorType = TargetCursorType.Harmful,
                    ClickedOnId = (Serial)0x40000022u
                }
            )
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(deathService.ForceDeathCalls, Is.EqualTo(0));
                Assert.That(output[^1], Is.EqualTo("Target is not a valid mobile."));
            }
        );
    }
}
