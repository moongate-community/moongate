using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Scripting.AI;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.AI;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;
using Serilog.Events;
using SquidStd.Core.Directories;
using SquidStd.Scripting.Lua.Services;
using ISynchronizeInvoke = System.ComponentModel.ISynchronizeInvoke;

namespace Moongate.Tests.Scripting;

[Collection(GlobalSerilogCollection.Name)]
public class LuaNpcBrainRuntimeTests
{
    private const string CounterBrain = """
                                        return {
                                          id = "counter",
                                          default_tick_ms = 1000,
                                          perception_range = 12,
                                          hearing_range = 15,
                                          think = function(ctx, state)
                                            state.count = (state.count or 0) + 1
                                            ai.say(tostring(state.count))
                                          end
                                        }
                                        """;

    private sealed class BrainRuntimeFixture : IDisposable
    {
        private readonly Container _container = new();
        private readonly DirectoriesConfig _directories;
        private readonly LuaScriptEngineService _engine;
        private readonly bool _queueWatcherCallbacks;
        private readonly string _root;
        private readonly string _scripts;
        private readonly List<string> _aiActions = [];

        public BrainContext Context { get; }

        public NpcAiMetrics Metrics { get; } = new();

        public StubEventBus Bus { get; } = new();

        public StubGameLoopContext Loop { get; } = new();

        public MutableTimeProvider Time { get; } = new(new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        public QueueingSynchronizer WatcherCallbacks { get; } = new();

        public List<ControllableFileSystemWatcher> Watchers { get; } = [];

        public List<string> AiSays { get; } = [];

        public IReadOnlyList<string> AiActions => _aiActions;

        public BrainMobileSnapshot Other { get; } = new(
            new(2),
            "Other",
            true,
            3,
            new(105, 205, 7),
            80,
            100,
            false,
            new(1),
            false,
            0
        );

        public LuaNpcBrainRuntime Runtime { get; }

        public BrainRuntimeFixture(int maxIntentsPerDecision = 8, bool queueWatcherCallbacks = false)
        {
            _queueWatcherCallbacks = queueWatcherCallbacks;
            _root = Path.Combine(Path.GetTempPath(), "mg-brain-" + Guid.NewGuid().ToString("N"));
            _directories = new(_root, ["scripts"]);
            _scripts = _directories.GetPath("scripts");
            _engine = new(
                _directories,
                _container,
                new(_root, _scripts, "MoongateTests", "1.0.0")
            );
            var config = new MoongateConfig
            {
                NpcAi = new()
                {
                    Advanced = new()
                    {
                        MinTickMilliseconds = 100,
                        MaxTickMilliseconds = 60_000,
                        MaxIntentsPerDecision = maxIntentsPerDecision,
                        InstructionBudget = 2_000
                    }
                }
            };
            Runtime = new(_engine, _directories, config, Metrics, Loop, Bus, Time, CreateWatcher);
            InstallRecordingAi();
            var self = new BrainMobileSnapshot(
                new(1),
                "Self",
                false,
                3,
                new(100, 200, 7),
                25,
                100,
                true,
                Serial.Zero,
                true,
                5
            );
            Context = new(
                DateTimeOffset.FromUnixTimeMilliseconds(1_714_566_896_789),
                self,
                3,
                new(90, 190, 0),
                [Other]
            );
        }

        public void Bind(uint mobileId, string brainId)
        {
            var result = Runtime.TryBind(new(mobileId), brainId, out _, out var error);

            Assert.True(result, error);
        }

        public void ClearAi()
        {
            AiSays.Clear();
            _aiActions.Clear();
        }

        public void CreateBrainSymbolicLink(string brainId, string targetContent)
        {
            var brainDirectory = Path.Combine(_scripts, "brains");
            Directory.CreateDirectory(brainDirectory);
            var targetPath = Path.Combine(_root, brainId + "-outside.lua");
            File.WriteAllText(targetPath, targetContent);
            File.CreateSymbolicLink(Path.Combine(brainDirectory, brainId + ".lua"), targetPath);
        }

        public void Dispose()
        {
            Runtime.Dispose();
            _engine.Dispose();
            _container.Dispose();
            Directory.Delete(_root, true);
        }

        public void InstallPrivilegedGlobal(string moduleName, string functionName, Action callback)
        {
            var module = new Table(_engine.LuaScript);
            module.Set(
                functionName,
                DynValue.NewCallback(
                    (_, _) =>
                    {
                        callback();

                        return DynValue.Nil;
                    }
                )
            );
            _engine.LuaScript.Globals.Set(moduleName, DynValue.NewTable(module));
        }

        public NpcBrainInvocationResult Think(uint mobileId)
            => Runtime.Invoke(new(mobileId), NpcBrainHookType.Think, Context);

        public void WriteBrain(string brainId, string content)
        {
            var brainDirectory = Path.Combine(_scripts, "brains");
            Directory.CreateDirectory(brainDirectory);
            File.WriteAllText(Path.Combine(brainDirectory, brainId + ".lua"), content);
        }

        public void WriteScript(string relativePath, string content)
            => File.WriteAllText(Path.Combine(_scripts, relativePath), content);

        private FileSystemWatcher CreateWatcher(string path)
        {
            var watcher = new ControllableFileSystemWatcher(path);

            if (_queueWatcherCallbacks)
            {
                watcher.SynchronizingObject = WatcherCallbacks;
            }

            Watchers.Add(watcher);

            return watcher;
        }

        private void InstallRecordingAi()
        {
            var ai = new Table(_engine.LuaScript);
            ai.Set(
                "say",
                DynValue.NewCallback(
                    (_, arguments) =>
                    {
                        AiSays.Add(arguments.Count > 0 ? arguments[0].ToPrintString() : "");
                        _aiActions.Add("say");

                        return DynValue.True;
                    }
                )
            );
            RecordAction(ai, "patrol");
            RecordAction(ai, "return_home");
            RecordAction(ai, "clear_target");
            RecordTargetAction(ai, "move_toward");
            RecordTargetAction(ai, "move_away");
            RecordTargetAction(ai, "engage");
            _engine.LuaScript.Globals.Set("ai", DynValue.NewTable(ai));
        }

        private void RecordAction(Table ai, string name)
            => ai.Set(
                name,
                DynValue.NewCallback(
                    (_, _) =>
                    {
                        _aiActions.Add(name);

                        return DynValue.True;
                    }
                )
            );

        private void RecordTargetAction(Table ai, string name)
            => ai.Set(
                name,
                DynValue.NewCallback(
                    (_, arguments) =>
                    {
                        _aiActions.Add(name + ":" + (arguments.Count > 0 ? arguments[0].ToPrintString() : ""));

                        return DynValue.True;
                    }
                )
            );
    }

    private sealed class ControllableFileSystemWatcher : FileSystemWatcher
    {
        public ControllableFileSystemWatcher(string path) : base(path) { }

        public void RaiseChanged(string fileName)
            => OnChanged(new(WatcherChangeTypes.Changed, Path, fileName));
    }

    private sealed class QueueingSynchronizer : ISynchronizeInvoke
    {
        private readonly Queue<Action> _pending = [];

        public bool InvokeRequired => true;

        public int PendingCount => _pending.Count;

        public IAsyncResult BeginInvoke(Delegate method, object?[]? arguments)
        {
            _pending.Enqueue(() => method.DynamicInvoke(arguments));

            return Task.CompletedTask;
        }

        public object? EndInvoke(IAsyncResult result)
            => null;

        public object? Invoke(Delegate method, object?[]? arguments)
            => method.DynamicInvoke(arguments);

        public void RunNext()
        {
            Assert.True(_pending.TryDequeue(out var callback));
            callback();
        }
    }

    [Fact]
    public void Invoke_AbsentOptionalHook_ReturnsSuccessWithoutNextTick()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");

        var result = fixture.Runtime.Invoke(new(1), NpcBrainHookType.Activate, fixture.Context);

        Assert.True(result.Success);
        Assert.Null(result.NextTickMs);
        Assert.Equal(0, fixture.Metrics.Current.HookFailures);
    }

    [Fact]
    public void Invoke_BlackboardsAreIsolatedPerMobile()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");
        fixture.Bind(2, "counter");

        fixture.Think(1);
        fixture.Think(2);

        Assert.Equal(new[] { "1", "1" }, fixture.AiSays);
    }

    [Fact]
    public void Invoke_ContextConversion_ExposesNeutralSnapshots()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain(
            "context",
            """
            return {
              id = "context",
              default_tick_ms = 1000,
              perception_range = 12,
              hearing_range = 15,
              think = function(ctx)
                local other = ctx.nearby[1]
                ai.say(
                  table.concat({
                    tostring(ctx.now_ms),
                    tostring(ctx.self.id),
                    ctx.self.name,
                    tostring(ctx.self.is_player),
                    tostring(ctx.self.map_id),
                    tostring(ctx.self.position.x),
                    tostring(ctx.self.position.y),
                    tostring(ctx.self.position.z),
                    tostring(ctx.self.hits),
                    tostring(ctx.self.hits_max),
                    tostring(ctx.self.hits_percent),
                    tostring(ctx.self.warmode),
                    tostring(ctx.self.combatant_id),
                    tostring(ctx.self.criminal),
                    tostring(ctx.self.kills),
                    tostring(ctx.home.map_id),
                    tostring(ctx.home.position.x),
                    tostring(other.id),
                    tostring(other.combatant_id),
                    tostring(#ctx.nearby)
                  }, "|")
                )
              end
            }
            """
        );
        fixture.Bind(1, "context");

        var result = fixture.Think(1);

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            "1714566896789|1|Self|false|3|100|200|7|25|100|25|true|nil|true|5|3|90|2|1|1",
            Assert.Single(fixture.AiSays)
        );
    }

    [Fact]
    public void Invoke_EventConversionAndHookMapping_ExposeExactFields()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain(
            "events",
            """
            local function say(value) ai.say(value) end
            return {
              id = "events",
              default_tick_ms = 1000,
              perception_range = 12,
              hearing_range = 15,
              on_activate = function() say("activate") end,
              on_deactivate = function() say("deactivate") end,
              on_speech_heard = function(ctx, state, event)
                say(table.concat({
                  tostring(event.speaker_id),
                  event.speaker_name,
                  tostring(event.speaker_is_player),
                  event.speech_type,
                  event.text
                }, "|"))
              end,
              on_mobile_entered_range = function(ctx, state, event)
                say("entered|" .. tostring(event.mobile_id) .. "|" ..
                  event.mobile_name .. "|" .. tostring(event.mobile_is_player) .. "|" ..
                  tostring(event.from_position.x) .. "|" .. tostring(event.to_position.y))
              end,
              on_mobile_left_range = function(ctx, state, event)
                say("left|" .. tostring(event.mobile_id))
              end,
              on_mobile_moved = function(ctx, state, event)
                say("moved|" .. tostring(event.mobile_id) .. "|" ..
                  tostring(event.from_position.x) .. "|" .. tostring(event.to_position.y))
              end,
              on_attacked = function(ctx, state, event)
                say("attacked|" .. tostring(event.attacker_id))
              end,
              on_damage = function(ctx, state, event)
                say("damage|" .. tostring(event.attacker_id) .. "|" .. tostring(event.amount))
              end,
              on_death = function(ctx, state, event)
                say("death|" .. tostring(event.killer_id))
              end,
              think = function() say("think") end
            }
            """
        );
        fixture.Bind(1, "events");
        var subject = fixture.Other;

        AssertHookText(fixture, NpcBrainHookType.Activate, null, "activate");
        AssertHookText(fixture, NpcBrainHookType.Deactivate, null, "deactivate");
        AssertHookText(
            fixture,
            NpcBrainHookType.SpeechHeard,
            new(NpcBrainEventType.SpeechHeard, subject, "hello", ChatMessageType.Regular),
            "2|Other|true|regular|hello"
        );
        AssertHookText(
            fixture,
            NpcBrainHookType.MobileEnteredRange,
            new(
                NpcBrainEventType.MobileEnteredRange,
                subject,
                FromPosition: new(5, 6, 0),
                ToPosition: new(7, 8, 0)
            ),
            "entered|2|Other|true|5|8"
        );
        AssertHookText(
            fixture,
            NpcBrainHookType.MobileLeftRange,
            new(NpcBrainEventType.MobileLeftRange, subject),
            "left|2"
        );
        AssertHookText(
            fixture,
            NpcBrainHookType.MobileMoved,
            new(
                NpcBrainEventType.MobileMoved,
                subject,
                FromPosition: new(10, 20, 1),
                ToPosition: new(30, 40, 2)
            ),
            "moved|2|10|40"
        );
        AssertHookText(
            fixture,
            NpcBrainHookType.Attacked,
            new(NpcBrainEventType.Attacked, subject),
            "attacked|2"
        );
        AssertHookText(
            fixture,
            NpcBrainHookType.Damage,
            new(NpcBrainEventType.Damage, subject, Amount: 17),
            "damage|2|17"
        );
        AssertHookText(
            fixture,
            NpcBrainHookType.Death,
            new(NpcBrainEventType.Death, subject),
            "death|2"
        );
        AssertHookText(fixture, NpcBrainHookType.Think, null, "think");
    }

    [Fact]
    public void Invoke_HookCanCallGlobalModules()
    {
        using var fixture = new BrainRuntimeFixture();
        var logged = 0;
        fixture.InstallPrivilegedGlobal("log", "info", () => logged++);
        fixture.WriteBrain(
            "logger",
            """
            return {
              id = "logger",
              default_tick_ms = 1000,
              perception_range = 12,
              hearing_range = 15,
              think = function()
                log.info("hello")
                return 750
              end
            }
            """
        );
        fixture.Bind(1, "logger");

        var result = fixture.Think(1);

        Assert.True(result.Success, result.Error);
        Assert.Equal(750, result.NextTickMs);
        Assert.Equal(1, logged);
    }

    [Fact]
    public void Invoke_InfiniteHook_ReturnsBudgetFailureRecordsMetricAndDoesNotLog()
    {
        using var logs = new GlobalSerilogCapture();
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain(
            "infinite",
            """
            return {
              id = "infinite",
              default_tick_ms = 1000,
              perception_range = 12,
              hearing_range = 15,
              think = function() while true do end end
            }
            """
        );
        fixture.Bind(1, "infinite");

        var result = fixture.Think(1);

        Assert.False(result.Success);
        Assert.True(result.InstructionBudgetExceeded);
        Assert.Null(result.NextTickMs);
        Assert.Equal(1, fixture.Metrics.Current.HookFailures);
        Assert.Equal(1, fixture.Metrics.Current.InstructionBudgetBreaches);
        Assert.DoesNotContain(logs.Events, logEvent => logEvent.Level >= LogEventLevel.Warning);
    }

    [Theory, InlineData("return 'text'"), InlineData("return true"), InlineData("return {}"), InlineData("return")]
    public void Invoke_NonNumericReturn_HasNullNextTick(string body)
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain(
            "noret",
            $$"""
              return {
                id = "noret",
                default_tick_ms = 1000,
                perception_range = 4,
                hearing_range = 4,
                think = function() {{body}} end
              }
              """
        );
        fixture.Bind(1, "noret");

        var result = fixture.Think(1);

        Assert.True(result.Success, result.Error);
        Assert.Null(result.NextTickMs);
    }

    [Fact]
    public void Invoke_NumericReturn_SetsNextTick()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain(
            "tick",
            """
            return {
              id = "tick",
              default_tick_ms = 1000,
              perception_range = 4,
              hearing_range = 4,
              think = function() return 750 end
            }
            """
        );
        fixture.Bind(1, "tick");

        var result = fixture.Think(1);

        Assert.True(result.Success, result.Error);
        Assert.Equal(750, result.NextTickMs);
    }

    [Fact]
    public void Invoke_ReusesOneMobilesBlackboard()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");

        fixture.Think(1);
        fixture.Think(1);

        Assert.Equal(new[] { "1", "2" }, fixture.AiSays);
    }

    [Fact]
    public void Invoke_StandardLibraryUsage_Works()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain(
            "native_normal",
            """
            return {
              id = "native_normal",
              default_tick_ms = 1000,
              perception_range = 12,
              hearing_range = 15,
              think = function()
                local values = { "a", "b" }
                table.insert(values, "c")
                local direct = string.rep(table.concat(values), 2)
                local colon = ("xy"):rep(2, ":")
                ai.say(direct .. "|" .. colon)
              end
            }
            """
        );
        fixture.Bind(1, "native_normal");

        var result = fixture.Think(1);

        Assert.True(result.Success, result.Error);
        Assert.Equal("abcabc|xy:xy", Assert.Single(fixture.AiSays));
    }

    [Fact]
    public void Invoke_UnknownHookValue_ReturnsNeutralFailure()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");

        var result = fixture.Runtime.Invoke(new(1), (NpcBrainHookType)999, fixture.Context);

        Assert.False(result.Success);
        Assert.Null(result.NextTickMs);
        Assert.Contains("unsupported", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, fixture.Metrics.Current.HookFailures);
    }

    [Fact]
    public void Reset_ClearsStateWithoutRemovingBinding()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");
        fixture.Think(1);

        fixture.Runtime.Reset(new(1));
        fixture.Think(1);

        Assert.True(fixture.Runtime.TryGetDescriptor(new(1), out _));
        Assert.Equal("1", fixture.AiSays[^1]);
    }

    [Fact]
    public async Task StartAsync_BuiltInBrainsExposeDeterministicDescriptorsAndBehavior()
    {
        using var fixture = new BrainRuntimeFixture();
        await fixture.Runtime.StartAsync();

        Assert.True(fixture.Runtime.TryBind(new(1), "guard", out var guard, out var guardError), guardError);
        Assert.Equal(new("guard", 1000, 12, 15), guard);

        fixture.ClearAi();
        fixture.Runtime.Invoke(
            new(1),
            NpcBrainHookType.SpeechHeard,
            fixture.Context,
            new(NpcBrainEventType.SpeechHeard, fixture.Context.Self, "hello", ChatMessageType.Regular)
        );
        Assert.Empty(fixture.AiSays);

        fixture.ClearAi();
        fixture.Runtime.Invoke(
            new(1),
            NpcBrainHookType.SpeechHeard,
            fixture.Context,
            new(NpcBrainEventType.SpeechHeard, fixture.Other, "hello", ChatMessageType.Regular)
        );
        Assert.Equal("I haven't seen you before.", Assert.Single(fixture.AiSays));

        fixture.ClearAi();
        fixture.Runtime.Invoke(
            new(1),
            NpcBrainHookType.SpeechHeard,
            fixture.Context,
            new(NpcBrainEventType.SpeechHeard, fixture.Other, "hello", ChatMessageType.Regular)
        );
        Assert.Equal("Welcome back.", Assert.Single(fixture.AiSays));

        fixture.ClearAi();
        var guardThink = fixture.Think(1);
        Assert.True(guardThink.Success, guardThink.Error);
        Assert.Empty(fixture.AiSays);
        Assert.Empty(fixture.AiActions);

        Assert.True(fixture.Runtime.TryBind(new(2), "orion", out var orion, out var orionError), orionError);
        Assert.Equal(new("orion", 1500, 10, 12), orion);
        fixture.ClearAi();
        fixture.Runtime.Invoke(new(2), NpcBrainHookType.Think, fixture.Context);
        Assert.Equal("patrol", Assert.Single(fixture.AiActions));

        Assert.True(fixture.Runtime.TryBind(new(3), "vega", out var vega, out var vegaError), vegaError);
        Assert.Equal(new("vega", 1500, 10, 12), vega);
        fixture.ClearAi();
        fixture.Runtime.Invoke(new(3), NpcBrainHookType.Think, fixture.Context);
        Assert.Equal("patrol", Assert.Single(fixture.AiActions));

        await fixture.Runtime.StopAsync();
    }

    [Fact]
    public async Task StartAsync_RapidFileChanges_PostOneReloadToTheGameLoop()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");
        var replacement = """
                          return {
                            id = "counter",
                            default_tick_ms = 1500,
                            perception_range = 10,
                            hearing_range = 12,
                            think = function(ctx, state) ai.say("watched") end
                          }
                          """;
        fixture.WriteBrain("counter", replacement);
        await fixture.Runtime.StartAsync();
        var watcher = Assert.Single(fixture.Watchers);

        watcher.RaiseChanged("counter.lua");
        watcher.RaiseChanged("counter.lua");
        watcher.RaiseChanged("counter.lua");

        fixture.Time.Advance(TimeSpan.FromMilliseconds(249));
        await DrainContinuationsAsync();
        Assert.Equal(0, fixture.Loop.PostCount);

        fixture.Time.Advance(TimeSpan.FromMilliseconds(1));
        await DrainContinuationsAsync(() => fixture.Loop.PostCount == 1);

        Assert.Equal(1, fixture.Loop.PostCount);
        Assert.Equal(1, fixture.Metrics.Current.ReloadSuccesses);
        Assert.Single(fixture.Bus.Published);
        fixture.Think(1);
        Assert.Equal("watched", fixture.AiSays[^1]);

        await fixture.Runtime.StopAsync();
    }

    [Fact]
    public async Task StartAsync_StoppedWatcherCallbackAfterRestart_DoesNotReload()
    {
        using var fixture = new BrainRuntimeFixture(queueWatcherCallbacks: true);
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");
        fixture.WriteBrain(
            "counter",
            """
            return {
              id = "counter",
              default_tick_ms = 1500,
              perception_range = 10,
              hearing_range = 12,
              think = function() ai.say("stale") end
            }
            """
        );
        await fixture.Runtime.StartAsync();
        var stoppedWatcher = Assert.Single(fixture.Watchers);
        stoppedWatcher.RaiseChanged("counter.lua");
        Assert.Equal(1, fixture.WatcherCallbacks.PendingCount);

        await fixture.Runtime.StopAsync();
        await fixture.Runtime.StartAsync();
        fixture.WatcherCallbacks.RunNext();
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await DrainContinuationsAsync();

        Assert.Equal(0, fixture.Loop.PostCount);
        Assert.Equal(0, fixture.Metrics.Current.ReloadSuccesses);
        Assert.Empty(fixture.Bus.Published);
        fixture.Think(1);
        Assert.Equal("1", fixture.AiSays[^1]);

        await fixture.Runtime.StopAsync();
    }

    [Fact]
    public void TryBind_DifferentBrain_ClearsState()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.WriteBrain(
            "reader",
            """
            return {
              id = "reader",
              default_tick_ms = 1000,
              perception_range = 12,
              hearing_range = 15,
              think = function(ctx, state)
                state.count = (state.count or 0) + 1
                ai.say(tostring(state.count))
              end
            }
            """
        );
        fixture.Bind(1, "counter");
        fixture.Think(1);

        fixture.Bind(1, "reader");
        fixture.Think(1);

        Assert.Equal("1", fixture.AiSays[^1]);
    }

    [Fact]
    public void TryBind_FileLargerThanLimit_IsRejected()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("oversized", new('-', 256 * 1024 + 1));

        var result = fixture.Runtime.TryBind(new(1), "oversized", out var descriptor, out var error);

        Assert.False(result);
        Assert.Null(descriptor);
        Assert.Contains("256", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryBind_InfiniteTopLevelChunk_ReturnsBudgetFailure()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("infinite", "while true do end");

        var result = fixture.Runtime.TryBind(new(1), "infinite", out var descriptor, out var error);

        Assert.False(result);
        Assert.Null(descriptor);
        Assert.Contains("budget", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, InlineData("../counter"), InlineData("Counter"), InlineData("-counter"), InlineData("counter.lua"),
     InlineData("counter/other")]
    public void TryBind_InvalidBrainId_IsRejected(string brainId)
    {
        using var fixture = new BrainRuntimeFixture();

        var result = fixture.Runtime.TryBind(new(1), brainId, out var descriptor, out var error);

        Assert.False(result);
        Assert.Null(descriptor);
        Assert.Contains("invalid", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, InlineData("other", 1000, 12, 15, "think = function() end", "id"),
     InlineData("invalid", 99, 12, 15, "think = function() end", "default_tick_ms"),
     InlineData("invalid", 60001, 12, 15, "think = function() end", "default_tick_ms"),
     InlineData("invalid", 1000, -1, 15, "think = function() end", "perception_range"),
     InlineData("invalid", 1000, 12, 65, "think = function() end", "hearing_range"),
     InlineData("invalid", 1000, 12, 15, "think = nil", "think"),
     InlineData("invalid", 1000, 12, 15, "think = function() end, on_activate = 42", "on_activate")]
    public void TryBind_InvalidDefinition_IsRejected(
        string returnedId,
        int defaultTickMilliseconds,
        int perceptionRange,
        int hearingRange,
        string hooks,
        string expectedError
    )
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain(
            "invalid",
            $$"""
              return {
                id = "{{returnedId}}",
                default_tick_ms = {{defaultTickMilliseconds}},
                perception_range = {{perceptionRange}},
                hearing_range = {{hearingRange}},
                {{hooks}}
              }
              """
        );

        var result = fixture.Runtime.TryBind(new(1), "invalid", out var descriptor, out var error);

        Assert.False(result);
        Assert.Null(descriptor);
        Assert.Contains(expectedError, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryBind_MissingFile_IsRejected()
    {
        using var fixture = new BrainRuntimeFixture();

        var result = fixture.Runtime.TryBind(new(1), "missing", out var descriptor, out var error);

        Assert.False(result);
        Assert.Null(descriptor);
        Assert.Contains("not found", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryBind_ResolvesOnlyTheExactBrainsDirectory()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteScript("counter.lua", CounterBrain);

        var outsideResult = fixture.Runtime.TryBind(new(1), "counter", out _, out var outsideError);

        Assert.False(outsideResult);
        Assert.NotNull(outsideError);

        fixture.WriteBrain("counter", CounterBrain);

        var result = fixture.Runtime.TryBind(new(1), "counter", out var descriptor, out var error);

        Assert.True(result, error);
        Assert.Equal(new("counter", 1000, 12, 15), descriptor);
    }

    [Fact]
    public void TryBind_SameBrainPreservesState()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");
        fixture.Think(1);

        fixture.Bind(1, "counter");
        fixture.Think(1);

        Assert.Equal("2", fixture.AiSays[^1]);
    }

    [Fact]
    public void TryBind_SymbolicLinkOutsideBrainsDirectory_IsRejected()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.CreateBrainSymbolicLink(
            "linked",
            """
            return {
              id = "linked",
              default_tick_ms = 1000,
              perception_range = 12,
              hearing_range = 15,
              think = function() end
            }
            """
        );

        var result = fixture.Runtime.TryBind(new(1), "linked", out var descriptor, out var error);

        Assert.False(result);
        Assert.Null(descriptor);
        Assert.Contains("link", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, InlineData("return {"), InlineData(
         """
         return {
           id = "other",
           default_tick_ms = 1000,
           perception_range = 12,
           hearing_range = 15,
           think = function() end
         }
         """
     ), InlineData(
         """
         return {
           id = "counter",
           default_tick_ms = 1000,
           perception_range = 12,
           hearing_range = 15
         }
         """
     )]
    public void TryReload_InvalidReplacement_PreservesBehaviorAndPublishesNothing(string replacement)
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");
        fixture.Think(1);
        fixture.WriteBrain("counter", replacement);

        var reloadResult = fixture.Runtime.TryReload("counter", out var descriptor, out var error);
        var invocation = fixture.Think(1);

        Assert.False(reloadResult);
        Assert.Null(descriptor);
        Assert.NotNull(error);
        Assert.True(invocation.Success, invocation.Error);
        Assert.Equal("2", fixture.AiSays[^1]);
        Assert.Equal(1, fixture.Metrics.Current.ReloadFallbacks);
        Assert.Empty(fixture.Bus.Published);
    }

    [Fact]
    public void TryReload_ValidReplacement_SwapsBehaviorPreservesStateAndPublishesOnce()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");
        fixture.Think(1);
        fixture.WriteBrain(
            "counter",
            """
            return {
              id = "counter",
              default_tick_ms = 1500,
              perception_range = 10,
              hearing_range = 12,
              think = function(ctx, state)
                state.count = (state.count or 0) + 1
                ai.say("reloaded:" .. tostring(state.count))
              end
            }
            """
        );

        var reloadResult = fixture.Runtime.TryReload("counter", out var descriptor, out var error);
        var invocation = fixture.Think(1);

        Assert.True(reloadResult, error);
        Assert.Equal(new("counter", 1500, 10, 12), descriptor);
        Assert.True(invocation.Success, invocation.Error);
        Assert.Equal("reloaded:2", fixture.AiSays[^1]);
        var reloaded = Assert.IsType<BrainDefinitionReloadedEvent>(Assert.Single(fixture.Bus.Published));
        Assert.Equal("counter", reloaded.BrainId);
        Assert.Equal(descriptor, reloaded.Descriptor);
        Assert.Equal(1, fixture.Metrics.Current.ReloadSuccesses);
        Assert.Equal(0, fixture.Metrics.Current.ReloadFallbacks);
    }

    [Fact]
    public void UnbindAndRebind_ClearsState()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");
        fixture.Think(1);

        fixture.Runtime.Unbind(new(1));

        Assert.False(fixture.Runtime.TryGetDescriptor(new(1), out _));

        fixture.Bind(1, "counter");
        fixture.Think(1);

        Assert.Equal("1", fixture.AiSays[^1]);
    }

    private static void AssertHookText(
        BrainRuntimeFixture fixture,
        NpcBrainHookType hook,
        NpcBrainEvent? brainEvent,
        string expected
    )
    {
        fixture.ClearAi();
        var result = fixture.Runtime.Invoke(new(1), hook, fixture.Context, brainEvent);

        Assert.True(result.Success, result.Error);
        Assert.Equal(expected, Assert.Single(fixture.AiSays));
    }

    private static async Task DrainContinuationsAsync(Func<bool>? condition = null)
    {
        for (var iteration = 0; iteration < 100 && condition?.Invoke() != true; iteration++)
        {
            await Task.Yield();
        }
    }
}
