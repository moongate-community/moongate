using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class LuaBrainRunnerTests
{
    private sealed class LuaBrainRegistryStub : ILuaBrainRegistry
    {
        private readonly Dictionary<string, LuaBrainDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

        public void Register(LuaBrainDefinition definition)
            => _definitions[definition.BrainId] = definition;

        public bool TryGet(string brainId, out LuaBrainDefinition? definition)
        {
            definition = null;

            if (_definitions.TryGetValue(brainId, out var resolved))
            {
                definition = resolved;

                return true;
            }

            return false;
        }
    }

    private sealed class LuaBrainRunnerTimerServiceSpy : ITimerService
    {
        public string? RegisteredTimerId { get; private set; }

        public bool Unregistered { get; private set; }

        public void ProcessTick() { }

        public string RegisterTimer(
            string name,
            TimeSpan interval,
            Action callback,
            TimeSpan? delay = null,
            bool repeat = false
        )
        {
            _ = name;
            _ = interval;
            _ = callback;
            _ = delay;
            _ = repeat;
            RegisteredTimerId = "lua_brain_runner_timer";

            return RegisteredTimerId;
        }

        public void UnregisterAllTimers() { }

        public bool UnregisterTimer(string timerId)
        {
            Unregistered = true;
            _ = timerId;

            return true;
        }

        public int UnregisterTimersByName(string name)
        {
            _ = name;

            return 0;
        }

        public int UpdateTicksDelta(long timestampMilliseconds)
        {
            _ = timestampMilliseconds;

            return 0;
        }
    }

    [Test]
    public async Task GetMetricsSnapshot_ShouldTrackDueProcessedDeferredAndTickTimings()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var config = new MoongateConfig
        {
            Scripting = new()
            {
                LuaBrainMaxBrainsPerTick = 1
            }
        };
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories, config);
        var firstNpc = new UOMobileEntity
        {
            Id = (Serial)0xB00,
            Name = "brain_1",
            BrainId = "orion",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var secondNpc = new UOMobileEntity
        {
            Id = (Serial)0xB01,
            Name = "brain_2",
            BrainId = "orion",
            MapId = 1,
            Location = new(101, 100, 0)
        };

        await runner.HandleAsync(new MobileAddedInWorldEvent(firstNpc, firstNpc.BrainId));
        await runner.HandleAsync(new MobileAddedInWorldEvent(secondNpc, secondNpc.BrainId));

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await runner.TickAllAsync(now);
        await runner.TickAllAsync(now + 1);
        var metrics = runner.GetMetricsSnapshot();

        Assert.Multiple(
            () =>
            {
                Assert.That(metrics.DueBrainsTotal, Is.EqualTo(3));
                Assert.That(metrics.ProcessedBrainsTotal, Is.EqualTo(2));
                Assert.That(metrics.DeferredBrainsTotal, Is.EqualTo(1));
                Assert.That(metrics.ProcessedTicksTotal, Is.EqualTo(2));
                Assert.That(metrics.TickDurationTotalMs, Is.GreaterThanOrEqualTo(0));
                Assert.That(metrics.TickDurationAvgMs, Is.GreaterThanOrEqualTo(0));
                Assert.That(metrics.TickDurationMaxMs, Is.GreaterThanOrEqualTo(0));
            }
        );
    }

    [Test]
    public async Task
        HandleAsync_WhenEnemyMobileUsesTemplateRangePerception_ShouldNotifyRangedGuardButNotMeleeGuard()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var archerGuard = new UOMobileEntity
        {
            Id = (Serial)0x720,
            Name = "archer_guard",
            BrainId = "guard",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var warriorGuard = new UOMobileEntity
        {
            Id = (Serial)0x721,
            Name = "warrior_guard",
            BrainId = "guard",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var zombie = new UOMobileEntity
        {
            Id = (Serial)0x722,
            Name = "zombie",
            IsPlayer = false,
            MapId = 1,
            Location = new(106, 100, 0),
            Notoriety = Notoriety.CanBeAttacked
        };

        archerGuard.SetCustomInteger(MobileCustomParamKeys.Ai.RangePerception, 10);
        warriorGuard.SetCustomInteger(MobileCustomParamKeys.Ai.RangePerception, 3);

        runner.Register(archerGuard, archerGuard.BrainId);
        runner.Register(warriorGuard, warriorGuard.BrainId);

        await runner.HandleAsync(new MobileAddedInWorldEvent(zombie, zombie.BrainId));
        await runner.TickAllAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var inRangeEvents = scriptEngine.Calls
                                        .Where(
                                            call => call.FunctionName == "on_event" &&
                                                    call.Args.Length > 0 &&
                                                    Equals(call.Args[0], "in_range")
                                        )
                                        .ToList();

        Assert.Multiple(
            () =>
            {
                Assert.That(inRangeEvents.Count, Is.EqualTo(1));

                if (inRangeEvents.Count == 1)
                {
                    Assert.That(inRangeEvents[0].Args[1], Is.EqualTo((uint)zombie.Id));
                }
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenEnemyMobileWithoutBrainIsAdded_ShouldPreserveEnemyPayload()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var guard = new UOMobileEntity
        {
            Id = (Serial)0x700,
            Name = "guard",
            BrainId = "guard",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var zombie = new UOMobileEntity
        {
            Id = (Serial)0x701,
            Name = "zombie",
            IsPlayer = false,
            MapId = 1,
            Location = new(101, 100, 0),
            Notoriety = Notoriety.CanBeAttacked
        };

        runner.Register(guard, guard.BrainId);
        await runner.HandleAsync(new MobileAddedInWorldEvent(zombie, zombie.BrainId));
        await runner.TickAllAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var eventCall = scriptEngine.Calls.First(
            call => call.FunctionName == "on_event" && call.Args.Length > 0 && Equals(call.Args[0], "in_range")
        );
        var payload = (Dictionary<string, object>)eventCall.Args[2]!;

        Assert.Multiple(
            () =>
            {
                Assert.That(payload["source_is_player"], Is.EqualTo(false));
                Assert.That(payload["source_is_enemy"], Is.EqualTo(true));
                Assert.That(payload["source_notoriety"], Is.EqualTo(nameof(Notoriety.CanBeAttacked)));
                Assert.That(payload["source_relation"], Is.EqualTo(nameof(AiRelation.Hostile)));
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenEnemyMobileWithoutBrainMovesIntoRange_ShouldPreserveEnemyPayload()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var guard = new UOMobileEntity
        {
            Id = (Serial)0x710,
            Name = "guard",
            BrainId = "guard",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var zombie = new UOMobileEntity
        {
            Id = (Serial)0x711,
            Name = "zombie",
            IsPlayer = false,
            MapId = 1,
            Location = new(120, 100, 0),
            Notoriety = Notoriety.CanBeAttacked
        };

        runner.Register(guard, guard.BrainId);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await runner.HandleAsync(new MobileAddedInWorldEvent(zombie, zombie.BrainId));
        await runner.TickAllAsync(now);
        scriptEngine.Calls.Clear();

        zombie.Location = new(102, 100, 0);
        await runner.HandleAsync(new MobilePositionChangedEvent(1, zombie.Id, 1, 1, new(120, 100, 0), zombie.Location));
        await runner.TickAllAsync(now + 1000);

        var eventCall = scriptEngine.Calls.First(
            call => call.FunctionName == "on_event" && call.Args.Length > 0 && Equals(call.Args[0], "in_range")
        );
        var payload = (Dictionary<string, object>)eventCall.Args[2]!;

        Assert.Multiple(
            () =>
            {
                Assert.That(payload["source_is_player"], Is.EqualTo(false));
                Assert.That(payload["source_is_enemy"], Is.EqualTo(true));
                Assert.That(payload["source_notoriety"], Is.EqualTo(nameof(Notoriety.CanBeAttacked)));
                Assert.That(payload["source_relation"], Is.EqualTo(nameof(AiRelation.Hostile)));
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenMobileAddedInWorldWithBrain_ShouldRegisterAndTick()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x60,
            Name = "orion",
            BrainId = "orion",
            MapId = 1,
            Location = new(100, 100, 0)
        };

        await runner.HandleAsync(new MobileAddedInWorldEvent(npc, npc.BrainId));
        await runner.TickAllAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var tickCall = scriptEngine.Calls.FirstOrDefault(call => call.FunctionName == "on_brain_tick");

        Assert.That(tickCall.FunctionName, Is.EqualTo("on_brain_tick"));
        Assert.That(tickCall.Args.Length, Is.EqualTo(1));
        Assert.That(tickCall.Args[0], Is.EqualTo((uint)npc.Id));
    }

    [Test]
    public async Task HandleAsync_WhenMobileMovesIntoNpcRange_ShouldInvokeOnEventInRange()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x500,
            Name = "watcher",
            BrainId = "watcher_brain",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var source = new UOMobileEntity
        {
            Id = (Serial)0x600,
            Name = "traveler",
            IsPlayer = false,
            BrainId = "traveler_brain",
            MapId = 1,
            Location = new(120, 100, 0)
        };
        runner.Register(npc, npc.BrainId);
        runner.Register(source, source.BrainId);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        source.Location = new(102, 100, 0);

        await runner.HandleAsync(
            new MobilePositionChangedEvent(
                1,
                source.Id,
                1,
                1,
                new(120, 100, 0),
                source.Location
            )
        );

        await runner.TickAllAsync(now);

        var eventCall = scriptEngine.Calls.FirstOrDefault(
            call => call.FunctionName == "on_event" && call.Args.Length > 0 && Equals(call.Args[0], "in_range")
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(eventCall.FunctionName, Is.EqualTo("on_event"));
                Assert.That(eventCall.Args.Length, Is.EqualTo(3));
                Assert.That(eventCall.Args[1], Is.EqualTo((uint)source.Id));
                Assert.That(eventCall.Args[2], Is.TypeOf<Dictionary<string, object>>());
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenMobileMovesOutNpcRange_ShouldInvokeOnEventOutRange()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x520,
            Name = "watcher",
            BrainId = "watcher_brain",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var source = new UOMobileEntity
        {
            Id = (Serial)0x620,
            Name = "traveler",
            IsPlayer = false,
            BrainId = "traveler_brain",
            MapId = 1,
            Location = new(102, 100, 0)
        };
        runner.Register(npc, npc.BrainId);
        runner.Register(source, source.BrainId);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Prime in-range state.
        await runner.HandleAsync(new MobilePositionChangedEvent(1, source.Id, 1, 1, new(120, 100, 0), source.Location));
        await runner.TickAllAsync(now);

        // Move out of range.
        source.Location = new(120, 100, 0);
        await runner.HandleAsync(new MobilePositionChangedEvent(1, source.Id, 1, 1, new(102, 100, 0), source.Location));
        await runner.TickAllAsync(now + 1000);

        var eventCall = scriptEngine.Calls.FirstOrDefault(
            call => call.FunctionName == "on_event" && call.Args.Length > 0 && Equals(call.Args[0], "out_range")
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(eventCall.FunctionName, Is.EqualTo("on_event"));
                Assert.That(eventCall.Args.Length, Is.EqualTo(3));
                Assert.That(eventCall.Args[1], Is.EqualTo((uint)source.Id));
                Assert.That(eventCall.Args[2], Is.TypeOf<Dictionary<string, object>>());
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenMobileStaysInNpcRange_ShouldNotInvokeDuplicateInRange()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x510,
            Name = "watcher",
            BrainId = "watcher_brain",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var source = new UOMobileEntity
        {
            Id = (Serial)0x610,
            Name = "traveler",
            IsPlayer = false,
            BrainId = "traveler_brain",
            MapId = 1,
            Location = new(120, 100, 0)
        };
        runner.Register(npc, npc.BrainId);
        runner.Register(source, source.BrainId);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        source.Location = new(102, 100, 0);
        await runner.HandleAsync(new MobilePositionChangedEvent(1, source.Id, 1, 1, new(120, 100, 0), source.Location));
        await runner.TickAllAsync(now);

        source.Location = new(101, 100, 0);
        await runner.HandleAsync(new MobilePositionChangedEvent(1, source.Id, 1, 1, new(102, 100, 0), source.Location));
        await runner.TickAllAsync(now + 1000);

        var inRangeCalls = scriptEngine.Calls.Count(
            call => call.FunctionName == "on_event" && call.Args.Length > 0 && Equals(call.Args[0], "in_range")
        );

        Assert.That(inRangeCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task StartAsync_AndStopAsync_ShouldRegisterAndUnregisterTimer()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);

        await runner.StartAsync();
        await runner.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(timerService.RegisteredTimerId, Is.Not.Null.And.Not.Empty);
                Assert.That(timerService.Unregistered, Is.True);
            }
        );
    }

    [Test]
    public async Task TickAllAsync_WhenBeforeAndAfterDeathAreQueued_ShouldInvokeSpecificHooks()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x62,
            Name = "orion",
            BrainId = "orion",
            MapId = 1,
            Location = new(100, 100, 0)
        };

        await runner.HandleAsync(new MobileAddedInWorldEvent(npc, npc.BrainId));
        runner.EnqueueDeath(
            npc.Id,
            new(
                LuaBrainDeathHookType.BeforeDeath,
                (Serial)0x08,
                new() { ["reason"] = "test_before" }
            )
        );
        runner.EnqueueDeath(
            npc.Id,
            new(
                LuaBrainDeathHookType.AfterDeath,
                (Serial)0x08,
                new() { ["reason"] = "test_after" }
            )
        );

        await runner.TickAllAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var beforeEventCall = scriptEngine.Calls.FirstOrDefault(
            call => call.FunctionName == "on_event" && call.Args.Length > 0 && Equals(call.Args[0], "before_death")
        );
        var beforeCall = scriptEngine.Calls.FirstOrDefault(call => call.FunctionName == "on_before_death");
        var afterEventCall = scriptEngine.Calls.FirstOrDefault(
            call => call.FunctionName == "on_event" && call.Args.Length > 0 && Equals(call.Args[0], "after_death")
        );
        var afterCall = scriptEngine.Calls.FirstOrDefault(call => call.FunctionName == "on_after_death");

        Assert.Multiple(
            () =>
            {
                Assert.That(beforeEventCall.FunctionName, Is.EqualTo("on_event"));
                Assert.That(beforeCall.FunctionName, Is.EqualTo("on_before_death"));
                Assert.That(afterEventCall.FunctionName, Is.EqualTo("on_event"));
                Assert.That(afterCall.FunctionName, Is.EqualTo("on_after_death"));
            }
        );
    }

    [Test]
    public async Task TickAllAsync_WhenCombatHitHookIsQueued_ShouldDispatchOnAttack()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x640,
            Name = "fighter",
            BrainId = "fighter_brain",
            MapId = 1,
            Location = new(100, 100, 0)
        };

        runner.Register(npc, npc.BrainId);
        runner.EnqueueCombatHook(
            npc.Id,
            new(
                LuaBrainCombatHookType.Attack,
                (Serial)0x777,
                new()
                {
                    ["damage"] = 6
                }
            )
        );

        await runner.TickAllAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var attackCall = scriptEngine.Calls.FirstOrDefault(call => call.FunctionName == "on_attack");

        Assert.Multiple(
            () =>
            {
                Assert.That(attackCall.FunctionName, Is.EqualTo("on_attack"));
                Assert.That(attackCall.Args.Length, Is.EqualTo(2));
                Assert.That(attackCall.Args[0], Is.EqualTo((uint)(Serial)0x777));
                Assert.That(attackCall.Args[1], Is.TypeOf<Dictionary<string, object?>>());
            }
        );
    }

    [Test]
    public async Task TickAllAsync_WhenCombatMissHookIsQueued_ShouldDispatchOnMissedByAttack()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x650,
            Name = "fighter",
            BrainId = "fighter_brain",
            MapId = 1,
            Location = new(100, 100, 0)
        };

        runner.Register(npc, npc.BrainId);
        runner.EnqueueCombatHook(
            npc.Id,
            new(
                LuaBrainCombatHookType.MissedByAttack,
                (Serial)0x778,
                new()
            )
        );

        await runner.TickAllAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var missCall = scriptEngine.Calls.FirstOrDefault(call => call.FunctionName == "on_missed_by_attack");

        Assert.Multiple(
            () =>
            {
                Assert.That(missCall.FunctionName, Is.EqualTo("on_missed_by_attack"));
                Assert.That(missCall.Args.Length, Is.EqualTo(2));
                Assert.That(missCall.Args[0], Is.EqualTo((uint)(Serial)0x778));
                Assert.That(missCall.Args[1], Is.TypeOf<Dictionary<string, object?>>());
            }
        );
    }

    [Test]
    public async Task TickAllAsync_WhenDeathIsQueued_ShouldInvokeOnDeathHooks()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x61,
            Name = "orion",
            BrainId = "orion",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var deathContext = new LuaBrainDeathContext(
            LuaBrainDeathHookType.Death,
            (Serial)0x07,
            new() { ["reason"] = "test" }
        );

        await runner.HandleAsync(new MobileAddedInWorldEvent(npc, npc.BrainId));
        runner.EnqueueDeath(npc.Id, deathContext);
        await runner.TickAllAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var onEventCall = scriptEngine.Calls.FirstOrDefault(
            call => call.FunctionName == "on_event" && call.Args.Length > 0 && Equals(call.Args[0], "death")
        );
        var onDeathCall = scriptEngine.Calls.FirstOrDefault(call => call.FunctionName == "on_death");

        Assert.Multiple(
            () =>
            {
                Assert.That(onEventCall.FunctionName, Is.EqualTo("on_event"));
                Assert.That(onEventCall.Args.Length, Is.EqualTo(3));
                Assert.That(onEventCall.Args[1], Is.EqualTo((uint)0x07));
                Assert.That(onEventCall.Args[2], Is.TypeOf<Dictionary<string, object?>>());
                Assert.That(onDeathCall.FunctionName, Is.EqualTo("on_death"));
                Assert.That(onDeathCall.Args.Length, Is.EqualTo(2));
                Assert.That(onDeathCall.Args[0], Is.EqualTo((uint)0x07));
                Assert.That(onDeathCall.Args[1], Is.TypeOf<Dictionary<string, object?>>());
            }
        );
    }

    [Test]
    public async Task TickAllAsync_WhenMaxBrainsPerTickIsConfigured_ShouldRespectProcessingBudget()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var config = new MoongateConfig
        {
            Scripting = new()
            {
                LuaBrainMaxBrainsPerTick = 1
            }
        };
        var runner = new LuaBrainRunner(
            timerService,
            scriptEngine,
            new LuaBrainRegistryStub(),
            directories,
            config
        );
        var firstNpc = new UOMobileEntity
        {
            Id = (Serial)0xA00,
            Name = "orion_1",
            BrainId = "orion",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var secondNpc = new UOMobileEntity
        {
            Id = (Serial)0xA01,
            Name = "orion_2",
            BrainId = "orion",
            MapId = 1,
            Location = new(102, 100, 0)
        };

        await runner.HandleAsync(new MobileAddedInWorldEvent(firstNpc, firstNpc.BrainId));
        await runner.HandleAsync(new MobileAddedInWorldEvent(secondNpc, secondNpc.BrainId));

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await runner.TickAllAsync(now);
        var firstTickCalls = scriptEngine.Calls.Count(call => call.FunctionName == "on_brain_tick");

        await runner.TickAllAsync(now + 1);
        var secondTickCalls = scriptEngine.Calls.Count(call => call.FunctionName == "on_brain_tick");

        Assert.Multiple(
            () =>
            {
                Assert.That(firstTickCalls, Is.EqualTo(1));
                Assert.That(secondTickCalls, Is.EqualTo(2));
            }
        );
    }

    [Test]
    public async Task TickAllAsync_WhenMultiplePendingEventsAreQueued_ShouldDispatchAllInSingleTick()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x900,
            Name = "orion",
            BrainId = "orion",
            MapId = 1,
            Location = new(100, 100, 0)
        };

        await runner.HandleAsync(new MobileAddedInWorldEvent(npc, npc.BrainId));
        await runner.HandleAsync(
            new SpeechHeardEvent(
                npc.Id,
                (Serial)0x02,
                "hello",
                ChatMessageType.Regular,
                1,
                new(101, 100, 0)
            )
        );
        await runner.HandleAsync(
            new MobileSpawnedFromSpawnerEvent(
                npc,
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "Spawner (test)",
                "Spawner (test)",
                "test-group",
                new(66, 1171, -28),
                1,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(2),
                0,
                5,
                10,
                "Nightmare",
                1,
                100
            )
        );
        runner.EnqueueDeath(
            npc.Id,
            new(LuaBrainDeathHookType.Death, (Serial)0x07, new() { ["reason"] = "test" })
        );

        await runner.TickAllAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var onEventNames = scriptEngine.Calls
                                       .Where(call => call.FunctionName == "on_event")
                                       .Select(call => call.Args[0]?.ToString())
                                       .ToList();
        var brainTickCalls = scriptEngine.Calls.Count(call => call.FunctionName == "on_brain_tick");

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    onEventNames.Count(name => string.Equals(name, "speech_heard", StringComparison.Ordinal)),
                    Is.EqualTo(1)
                );
                Assert.That(
                    onEventNames.Count(name => string.Equals(name, "spawn", StringComparison.Ordinal)),
                    Is.EqualTo(1)
                );
                Assert.That(
                    onEventNames.Count(name => string.Equals(name, "death", StringComparison.Ordinal)),
                    Is.EqualTo(1)
                );
                Assert.That(brainTickCalls, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task TickAllAsync_WhenSpawnIsQueued_ShouldInvokeOnSpawnCallback()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var runner = new LuaBrainRunner(timerService, scriptEngine, new LuaBrainRegistryStub(), directories);
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x70,
            Name = "spawned_orc",
            BrainId = "orc_warrior",
            MapId = 1,
            Location = new(150, 150, 0)
        };

        await runner.HandleAsync(new MobileAddedInWorldEvent(npc, npc.BrainId));
        await runner.HandleAsync(
            new MobileSpawnedFromSpawnerEvent(
                npc,
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Spawner (302)",
                "Ilshenar",
                "shrine-spawn",
                new(66, 1171, -28),
                1,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(10),
                0,
                5,
                10,
                "Nightmare",
                1,
                100
            )
        );
        await runner.TickAllAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var onSpawnCall = scriptEngine.Calls.FirstOrDefault(call => call.FunctionName == "on_spawn");
        var onEventCall = scriptEngine.Calls.FirstOrDefault(
            call => call.FunctionName == "on_event" && call.Args.Length > 0 && Equals(call.Args[0], "spawn")
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(onSpawnCall.FunctionName, Is.EqualTo("on_spawn"));
                Assert.That(onSpawnCall.Args.Length, Is.EqualTo(2));
                Assert.That(onSpawnCall.Args[0], Is.EqualTo((uint)npc.Id));
                Assert.That(onSpawnCall.Args[1], Is.TypeOf<Dictionary<string, object>>());
                Assert.That(onEventCall.FunctionName, Is.EqualTo("on_event"));
                Assert.That(onEventCall.Args.Length, Is.EqualTo(3));
                Assert.That(onEventCall.Args[0], Is.EqualTo("spawn"));
            }
        );
    }

    [Test]
    public async Task TickAllAsync_WhenSpeechIsQueued_ShouldInvokeOnEventCallback()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptPath = Path.Combine(directories[DirectoryType.Scripts], "ai", "orc_warrior.lua");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, "function on_event() end");
        var registry = new LuaBrainRegistryStub();
        registry.Register(new() { BrainId = "orc_warrior", ScriptPath = scriptPath });
        var runner = new LuaBrainRunner(timerService, scriptEngine, registry, directories);
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x50,
            Name = "orc",
            MapId = 1,
            Location = new(100, 100, 0)
        };

        runner.Register(npc, "orc_warrior");
        await runner.HandleAsync(
            new SpeechHeardEvent(
                (Serial)0x50,
                (Serial)0x02,
                "hello",
                ChatMessageType.Regular,
                1,
                new(101, 100, 0)
            )
        );

        await runner.TickAllAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var eventCall = scriptEngine.Calls.FirstOrDefault(call => call.FunctionName == "on_event");
        var speechCall = scriptEngine.Calls.FirstOrDefault(call => call.FunctionName == "on_speech");

        Assert.Multiple(
            () =>
            {
                Assert.That(eventCall.FunctionName, Is.EqualTo("on_event"));
                Assert.That(eventCall.Args.Length, Is.EqualTo(3));
                Assert.That(eventCall.Args[0], Is.EqualTo("speech_heard"));
                Assert.That(eventCall.Args[1], Is.EqualTo((uint)0x02));
                Assert.That(eventCall.Args[2], Is.TypeOf<Dictionary<string, object>>());
                Assert.That(speechCall.FunctionName, Is.EqualTo("on_speech"));
                Assert.That(speechCall.Args.Length, Is.GreaterThanOrEqualTo(8));
            }
        );
    }
}
