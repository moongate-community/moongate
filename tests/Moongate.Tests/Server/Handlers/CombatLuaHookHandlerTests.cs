using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Handlers;

public sealed class CombatLuaHookHandlerTests
{
    private sealed class LuaBrainRunnerSpy : ILuaBrainRunner
    {
        public List<(Serial MobileId, LuaBrainCombatHookContext Context)> CombatHooks { get; } = [];

        public void EnqueueCombatHook(Serial mobileId, LuaBrainCombatHookContext combatContext)
            => CombatHooks.Add((mobileId, combatContext));

        public void EnqueueDeath(Serial mobileId, LuaBrainDeathContext deathContext)
            => throw new NotSupportedException();

        public void EnqueueInRange(Serial listenerNpcId, UOMobileEntity sourceMobile, int range = 3)
            => throw new NotSupportedException();

        public void EnqueueSpawn(MobileSpawnedFromSpawnerEvent gameEvent)
            => throw new NotSupportedException();

        public void EnqueueSpeech(SpeechHeardEvent gameEvent)
            => throw new NotSupportedException();

        public IReadOnlyList<LuaBrainContextMenuEntry> GetContextMenuEntries(UOMobileEntity mobile, UOMobileEntity? requester)
            => [];

        public void Register(UOMobileEntity mobile, string brainId)
            => throw new NotSupportedException();

        public ValueTask TickAllAsync(long nowMilliseconds, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public bool TryHandleContextMenuSelection(UOMobileEntity mobile, UOMobileEntity? requester, string menuKey, long sessionId)
            => false;

        public void Unregister(Serial mobileId) { }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;

        public Task HandleAsync(SpeechHeardEvent gameEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task HandleAsync(MobileAddedInWorldEvent gameEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task HandleAsync(MobileSpawnedFromSpawnerEvent gameEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    [Test]
    public async Task HandleAsync_WhenHitOccurs_ShouldEnqueueAttackAndAttackedHooksForNpcBrains()
    {
        var runner = new LuaBrainRunnerSpy();
        var handler = new CombatLuaHookHandler(runner);
        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x0100,
            IsPlayer = false,
            MapId = 1,
            Location = new Point3D(100, 100, 0)
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x0200,
            IsPlayer = false,
            MapId = 1,
            Location = new Point3D(101, 100, 0)
        };

        await handler.HandleAsync(new CombatHitEvent(attacker.Id, defender.Id, attacker.MapId, attacker.Location, 6, attacker, defender));

        Assert.Multiple(
            () =>
            {
                Assert.That(runner.CombatHooks, Has.Count.EqualTo(2));
                Assert.That(runner.CombatHooks[0].MobileId, Is.EqualTo(attacker.Id));
                Assert.That(runner.CombatHooks[0].Context.HookType, Is.EqualTo(LuaBrainCombatHookType.Attack));
                Assert.That(runner.CombatHooks[1].MobileId, Is.EqualTo(defender.Id));
                Assert.That(runner.CombatHooks[1].Context.HookType, Is.EqualTo(LuaBrainCombatHookType.Attacked));
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenMissOccurs_ShouldEnqueueMissHooksForNpcBrains()
    {
        var runner = new LuaBrainRunnerSpy();
        var handler = new CombatLuaHookHandler(runner);
        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x0100,
            IsPlayer = false,
            MapId = 1,
            Location = new Point3D(100, 100, 0)
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x0200,
            IsPlayer = false,
            MapId = 1,
            Location = new Point3D(101, 100, 0)
        };

        await handler.HandleAsync(new CombatMissEvent(attacker.Id, defender.Id, attacker.MapId, attacker.Location, attacker, defender));

        Assert.Multiple(
            () =>
            {
                Assert.That(runner.CombatHooks, Has.Count.EqualTo(2));
                Assert.That(runner.CombatHooks[0].Context.HookType, Is.EqualTo(LuaBrainCombatHookType.MissedAttack));
                Assert.That(runner.CombatHooks[1].Context.HookType, Is.EqualTo(LuaBrainCombatHookType.MissedByAttack));
            }
        );
    }
}
