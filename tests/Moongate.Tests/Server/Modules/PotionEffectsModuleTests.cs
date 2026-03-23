using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Modules;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Modules;

public sealed class PotionEffectsModuleTests
{
    private sealed class PotionEffectsModuleTestBackgroundJobService : IBackgroundJobService
    {
        private readonly Queue<Func<Task>> _backgroundJobs = new();
        private readonly Queue<Action> _gameLoopActions = new();

        public void EnqueueBackground(Action job)
            => _backgroundJobs.Enqueue(() =>
            {
                job();
                return Task.CompletedTask;
            });

        public void EnqueueBackground(Func<Task> job)
            => _backgroundJobs.Enqueue(job);

        public int ExecutePendingOnGameLoop(int maxActions = 100)
        {
            var executed = 0;

            while (executed < maxActions && _gameLoopActions.Count > 0)
            {
                _gameLoopActions.Dequeue()();
                executed++;
            }

            return executed;
        }

        public void PostToGameLoop(Action action)
            => _gameLoopActions.Enqueue(action);

        public void RunBackgroundAndPostResult<TResult>(
            Func<TResult> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
            => throw new NotSupportedException();

        public void RunBackgroundAndPostResultAsync<TResult>(
            Func<Task<TResult>> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
            => throw new NotSupportedException();

        public void Start(int? workerCount = null)
            => throw new NotSupportedException();

        public Task StopAsync()
            => Task.CompletedTask;

        public void DrainBackground()
        {
            while (_backgroundJobs.Count > 0)
            {
                _backgroundJobs.Dequeue()().GetAwaiter().GetResult();
            }
        }

        public void DrainGameLoop()
            => _ = ExecutePendingOnGameLoop(int.MaxValue);
    }

    [Test]
    public void RestoreHits_WhenMobileIsOnline_ShouldClampToMaxHitsAndEnqueueStatusRefresh()
    {
        var sessionService = new FakeGameNetworkSessionService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var module = new PotionEffectsModule(sessionService, outgoingQueue);
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x401u,
            Name = "Potion Tester",
            IsAlive = true,
            Hits = 30,
            MaxHits = 40
        };

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = mobile.Id,
            Character = mobile
        };
        sessionService.Add(session);

        var restored = module.RestoreHits(0x401, 25);
        var dequeued = outgoingQueue.TryDequeue(out var packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(restored, Is.True);
                Assert.That(mobile.Hits, Is.EqualTo(40));
                Assert.That(dequeued, Is.True);
                Assert.That(packet.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(packet.Packet, Is.TypeOf<PlayerStatusPacket>());
            }
        );
    }

    [Test]
    public void RestoreStamina_WhenAmountExceedsMax_ShouldClampToMaxStamina()
    {
        var sessionService = new FakeGameNetworkSessionService();
        var module = new PotionEffectsModule(sessionService);
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x402u,
            Name = "Potion Tester",
            IsAlive = true,
            Stamina = 15,
            MaxStamina = 20
        };

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = mobile.Id,
            Character = mobile
        };
        sessionService.Add(session);

        var restored = module.RestoreStamina(0x402, 25);

        Assert.Multiple(
            () =>
            {
                Assert.That(restored, Is.True);
                Assert.That(mobile.Stamina, Is.EqualTo(20));
            }
        );
    }

    [Test]
    public void ApplyTemporaryStrength_WhenDurationExpires_ShouldAddThenRemoveRuntimeModifier()
    {
        var sessionService = new FakeGameNetworkSessionService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var backgroundJobs = new PotionEffectsModuleTestBackgroundJobService();
        var module = new PotionEffectsModule(sessionService, outgoingQueue, backgroundJobService: backgroundJobs);
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x403u,
            Name = "Potion Tester",
            IsAlive = true,
            Strength = 50,
            Hits = 50,
            MaxHits = 50
        };

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = mobile.Id,
            Character = mobile
        };
        sessionService.Add(session);

        var applied = module.ApplyTemporaryStrength(0x403, 10, 0);

        Assert.Multiple(
            () =>
            {
                Assert.That(applied, Is.True);
                Assert.That(mobile.RuntimeModifiers, Is.Not.Null);
                Assert.That(mobile.RuntimeModifiers!.StrengthBonus, Is.EqualTo(10));
                Assert.That(mobile.EffectiveStrength, Is.EqualTo(60));
            }
        );

        backgroundJobs.DrainBackground();
        backgroundJobs.DrainGameLoop();

        Assert.That(mobile.RuntimeModifiers, Is.Null);
        Assert.That(mobile.EffectiveStrength, Is.EqualTo(50));

        var packets = new List<OutgoingGamePacket>();
        while (outgoingQueue.TryDequeue(out var packet))
        {
            packets.Add(packet);
        }

        Assert.That(packets.Count(packet => packet.Packet is PlayerStatusPacket), Is.EqualTo(2));
    }
}
