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
            IsPlayer = true,
            MapId = 1,
            Location = new(120, 100, 0)
        };
        runner.Register(npc, npc.BrainId);
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
            IsPlayer = true,
            MapId = 1,
            Location = new(102, 100, 0)
        };
        runner.Register(npc, npc.BrainId);
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
            IsPlayer = true,
            MapId = 1,
            Location = new(120, 100, 0)
        };
        runner.Register(npc, npc.BrainId);
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
            new((Serial)0x07, new() { ["reason"] = "test" })
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
