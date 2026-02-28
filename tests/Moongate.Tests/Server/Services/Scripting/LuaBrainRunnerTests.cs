using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Services.Scripting;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
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
    public async Task TickAllAsync_WhenSpeechIsQueued_ShouldInvokeOnSpeechCallback()
    {
        using var temp = new TempDirectory();
        var timerService = new LuaBrainRunnerTimerServiceSpy();
        var scriptEngine = new ItemScriptDispatcherTestScriptEngineService();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptPath = Path.Combine(directories[DirectoryType.Scripts], "ai", "orc_warrior.lua");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, "function on_speech() end");
        var registry = new LuaBrainRegistryStub();
        registry.Register(new() { BrainId = "orc_warrior", ScriptPath = scriptPath });
        var runner = new LuaBrainRunner(timerService, scriptEngine, registry, directories);
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x50,
            Name = "orc",
            MapId = 1,
            Location = new Point3D(100, 100, 0)
        };

        runner.Register(npc, "orc_warrior");
        await runner.HandleAsync(
            new SpeechHeardEvent(
                (Serial)0x50,
                (Serial)0x02,
                "hello",
                ChatMessageType.Regular,
                1,
                new Point3D(101, 100, 0)
            )
        );

        await runner.TickAllAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var speechCall = scriptEngine.Calls.FirstOrDefault(call => call.FunctionName == "on_speech");

        Assert.Multiple(
            () =>
            {
                Assert.That(speechCall.FunctionName, Is.EqualTo("on_speech"));
                Assert.That(speechCall.Args.Length, Is.GreaterThanOrEqualTo(8));
            }
        );
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
}
