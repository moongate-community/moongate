using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.AI;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.AI;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;
using SquidStd.Core.Directories;
using SquidStd.Scripting.Lua.Data.Config;
using SquidStd.Scripting.Lua.Services;

namespace Moongate.Tests.Scripting;

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
                                                  return brain.say(tostring(state.count))
                                                end
                                              }
                                              """;

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

    [Theory]
    [InlineData("../counter")]
    [InlineData("Counter")]
    [InlineData("-counter")]
    [InlineData("counter.lua")]
    [InlineData("counter/other")]
    public void TryBind_InvalidBrainId_IsRejected(string brainId)
    {
        using var fixture = new BrainRuntimeFixture();

        var result = fixture.Runtime.TryBind(new(1), brainId, out var descriptor, out var error);

        Assert.False(result);
        Assert.Null(descriptor);
        Assert.Contains("invalid", error, StringComparison.OrdinalIgnoreCase);
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

    [Theory]
    [InlineData(
        "other",
        1000,
        12,
        15,
        "think = function() end",
        "id"
    )]
    [InlineData(
        "invalid",
        99,
        12,
        15,
        "think = function() end",
        "default_tick_ms"
    )]
    [InlineData(
        "invalid",
        60001,
        12,
        15,
        "think = function() end",
        "default_tick_ms"
    )]
    [InlineData(
        "invalid",
        1000,
        -1,
        15,
        "think = function() end",
        "perception_range"
    )]
    [InlineData(
        "invalid",
        1000,
        12,
        65,
        "think = function() end",
        "hearing_range"
    )]
    [InlineData(
        "invalid",
        1000,
        12,
        15,
        "think = nil",
        "think"
    )]
    [InlineData(
        "invalid",
        1000,
        12,
        15,
        "think = function() end, on_activate = 42",
        "on_activate"
    )]
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
    public void TryBind_FileLargerThanLimit_IsRejected()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("oversized", new string('-', (256 * 1024) + 1));

        var result = fixture.Runtime.TryBind(new(1), "oversized", out var descriptor, out var error);

        Assert.False(result);
        Assert.Null(descriptor);
        Assert.Contains("256", error, StringComparison.OrdinalIgnoreCase);
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
              think = function() return brain.idle() end
            }
            """
        );

        var result = fixture.Runtime.TryBind(new(1), "linked", out var descriptor, out var error);

        Assert.False(result);
        Assert.Null(descriptor);
        Assert.Contains("link", error, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void Invoke_BlackboardsAreIsolatedPerMobile()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");
        fixture.Bind(2, "counter");

        var first = fixture.Think(1);
        var second = fixture.Think(2);

        Assert.Equal("1", Assert.Single(first.Decision.Intents).Text);
        Assert.Equal("1", Assert.Single(second.Decision.Intents).Text);
    }

    [Fact]
    public void Invoke_ReusesOneMobilesBlackboard()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");

        var first = fixture.Think(1);
        var second = fixture.Think(1);

        Assert.Equal("1", Assert.Single(first.Decision.Intents).Text);
        Assert.Equal("2", Assert.Single(second.Decision.Intents).Text);
    }

    [Fact]
    public void Reset_ClearsStateWithoutRemovingBinding()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");
        fixture.Think(1);

        fixture.Runtime.Reset(new(1));
        var result = fixture.Think(1);

        Assert.True(fixture.Runtime.TryGetDescriptor(new(1), out _));
        Assert.Equal("1", Assert.Single(result.Decision.Intents).Text);
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
        var result = fixture.Think(1);

        Assert.Equal("1", Assert.Single(result.Decision.Intents).Text);
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
                return brain.say(tostring(state.count))
              end
            }
            """
        );
        fixture.Bind(1, "counter");
        fixture.Think(1);

        fixture.Bind(1, "reader");
        var result = fixture.Think(1);

        Assert.Equal("1", Assert.Single(result.Decision.Intents).Text);
    }

    [Fact]
    public void TryBind_SameBrainPreservesState()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");
        fixture.Think(1);

        fixture.Bind(1, "counter");
        var result = fixture.Think(1);

        Assert.Equal("2", Assert.Single(result.Decision.Intents).Text);
    }

    [Fact]
    public void Invoke_InfiniteHook_ReturnsBudgetFailureAndRecordsMetric()
    {
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
        Assert.Empty(result.Decision.Intents);
        Assert.Equal(1, fixture.Metrics.Current.HookFailures);
        Assert.Equal(1, fixture.Metrics.Current.InstructionBudgetBreaches);
    }

    [Fact]
    public void Invoke_AbsentOptionalHook_ReturnsSuccessfulEmptyDecision()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");

        var result = fixture.Runtime.Invoke(new(1), NpcBrainHookType.Activate, fixture.Context);

        Assert.True(result.Success);
        Assert.Equal(BrainDecision.Empty, result.Decision);
        Assert.Equal(0, fixture.Metrics.Current.HookFailures);
    }

    [Fact]
    public void Invoke_UnknownHookValue_ReturnsNeutralFailure()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");

        var result = fixture.Runtime.Invoke(new(1), (NpcBrainHookType)999, fixture.Context);

        Assert.False(result.Success);
        Assert.Empty(result.Decision.Intents);
        Assert.Contains("unsupported", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, fixture.Metrics.Current.HookFailures);
    }

    [Fact]
    public void Invoke_PrivilegedGlobalsAreUnavailableAndCannotMutateWorld()
    {
        using var fixture = new BrainRuntimeFixture();
        var mutationCount = 0;
        fixture.InstallPrivilegedGlobal("mobile", "set", () => mutationCount++);
        fixture.InstallPrivilegedGlobal("chat", "say", () => mutationCount++);
        fixture.InstallPrivilegedGlobal("game", "post", () => mutationCount++);
        fixture.InstallPrivilegedGlobal("events", "on", () => mutationCount++);
        fixture.WriteBrain(
            "sandbox",
            """
            return {
              id = "sandbox",
              default_tick_ms = 1000,
              perception_range = 12,
              hearing_range = 15,
              think = function()
                local mobile_ok = pcall(function() mobile.set() end)
                local chat_ok = pcall(function() chat.say() end)
                local game_ok = pcall(function() game.post() end)
                local events_ok = pcall(function() events.on() end)
                return brain.say(
                  tostring(mobile_ok) .. "," ..
                  tostring(chat_ok) .. "," ..
                  tostring(game_ok) .. "," ..
                  tostring(events_ok)
                )
              end
            }
            """
        );
        fixture.Bind(1, "sandbox");

        var result = fixture.Think(1);

        Assert.True(result.Success, result.Error);
        Assert.Equal("false,false,false,false", Assert.Single(result.Decision.Intents).Text);
        Assert.Equal(0, mutationCount);
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
                return brain.say(
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
            Assert.Single(result.Decision.Intents).Text
        );
    }

    [Fact]
    public void Invoke_EventConversionAndHookMapping_ExposeExactFields()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain(
            "events",
            """
            local function say(value) return brain.say(value) end
            return {
              id = "events",
              default_tick_ms = 1000,
              perception_range = 12,
              hearing_range = 15,
              on_activate = function() return say("activate") end,
              on_deactivate = function() return say("deactivate") end,
              on_speech_heard = function(ctx, state, event)
                return say(table.concat({
                  tostring(event.speaker_id),
                  event.speaker_name,
                  tostring(event.speaker_is_player),
                  event.speech_type,
                  event.text
                }, "|"))
              end,
              on_mobile_entered_range = function(ctx, state, event)
                return say("entered|" .. tostring(event.mobile_id) .. "|" ..
                  event.mobile_name .. "|" .. tostring(event.mobile_is_player) .. "|" ..
                  tostring(event.from_position.x) .. "|" .. tostring(event.to_position.y))
              end,
              on_mobile_left_range = function(ctx, state, event)
                return say("left|" .. tostring(event.mobile_id))
              end,
              on_mobile_moved = function(ctx, state, event)
                return say("moved|" .. tostring(event.mobile_id) .. "|" ..
                  tostring(event.from_position.x) .. "|" .. tostring(event.to_position.y))
              end,
              on_attacked = function(ctx, state, event)
                return say("attacked|" .. tostring(event.attacker_id))
              end,
              on_damage = function(ctx, state, event)
                return say("damage|" .. tostring(event.attacker_id) .. "|" .. tostring(event.amount))
              end,
              on_death = function(ctx, state, event)
                return say("death|" .. tostring(event.killer_id))
              end,
              think = function() return say("think") end
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
    public void Invoke_BrainHelpersAndDecisionConversion_MapAndClampValues()
    {
        using var fixture = new BrainRuntimeFixture(maxIntentsPerDecision: 20);
        fixture.WriteBrain(
            "helpers",
            """
            return {
              id = "helpers",
              default_tick_ms = 1000,
              perception_range = 12,
              hearing_range = 15,
              think = function()
                return brain.decision(1, {
                  brain.idle(),
                  brain.say("hello"),
                  brain.patrol(),
                  brain.move_toward(2),
                  brain.move_away(3),
                  brain.engage(4),
                  brain.clear_target(),
                  brain.return_home()
                })
              end
            }
            """
        );
        fixture.Bind(1, "helpers");

        var result = fixture.Think(1);

        Assert.True(result.Success, result.Error);
        Assert.Equal(100, result.Decision.NextTickMilliseconds);
        Assert.Collection(
            result.Decision.Intents,
            intent => Assert.Equal(BrainIntentType.Idle, intent.Type),
            intent =>
            {
                Assert.Equal(BrainIntentType.Say, intent.Type);
                Assert.Equal("hello", intent.Text);
            },
            intent => Assert.Equal(BrainIntentType.Patrol, intent.Type),
            intent =>
            {
                Assert.Equal(BrainIntentType.MoveToward, intent.Type);
                Assert.Equal(new Serial(2), intent.TargetId);
            },
            intent =>
            {
                Assert.Equal(BrainIntentType.MoveAway, intent.Type);
                Assert.Equal(new Serial(3), intent.TargetId);
            },
            intent =>
            {
                Assert.Equal(BrainIntentType.Engage, intent.Type);
                Assert.Equal(new Serial(4), intent.TargetId);
            },
            intent => Assert.Equal(BrainIntentType.ClearTarget, intent.Type),
            intent => Assert.Equal(BrainIntentType.ReturnHome, intent.Type)
        );
    }

    [Fact]
    public void Invoke_DecisionConversion_AcceptsNilSingleIntentAndUnknownIntents()
    {
        using var fixture = new BrainRuntimeFixture(maxIntentsPerDecision: 2);
        fixture.WriteBrain(
            "conversion",
            """
            return {
              id = "conversion",
              default_tick_ms = 1000,
              perception_range = 12,
              hearing_range = 15,
              on_activate = function() return nil end,
              on_deactivate = function() return brain.say("single") end,
              think = function()
                return brain.decision(999999, {
                  { type = "teleport", target_id = "bad" },
                  { text = "missing type" },
                  brain.say("truncated")
                })
              end
            }
            """
        );
        fixture.Bind(1, "conversion");

        var nilResult = fixture.Runtime.Invoke(new(1), NpcBrainHookType.Activate, fixture.Context);
        var singleResult = fixture.Runtime.Invoke(new(1), NpcBrainHookType.Deactivate, fixture.Context);
        var decisionResult = fixture.Think(1);

        Assert.True(nilResult.Success);
        Assert.Equal(BrainDecision.Empty, nilResult.Decision);
        Assert.Equal(BrainIntentType.Say, Assert.Single(singleResult.Decision.Intents).Type);
        Assert.Equal("single", Assert.Single(singleResult.Decision.Intents).Text);
        Assert.Equal(60_000, decisionResult.Decision.NextTickMilliseconds);
        Assert.Collection(
            decisionResult.Decision.Intents,
            intent =>
            {
                Assert.Equal(BrainIntentType.Unknown, intent.Type);
                Assert.Equal("teleport", intent.RawType);
            },
            intent =>
            {
                Assert.Equal(BrainIntentType.Unknown, intent.Type);
                Assert.Equal("", intent.RawType);
            }
        );
    }

    [Theory]
    [InlineData("return 'unsupported'")]
    [InlineData("return 42")]
    [InlineData("return true")]
    [InlineData("return 'first', 'second'")]
    [InlineData("return function() end")]
    [InlineData("return brain.say")]
    public void Invoke_UnsupportedReturnShape_ReturnsNeutralFailure(string returnStatement)
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain(
            "unsupported",
            $$"""
              return {
                id = "unsupported",
                default_tick_ms = 1000,
                perception_range = 12,
                hearing_range = 15,
                think = function()
                  {{returnStatement}}
                end
              }
              """
        );
        fixture.Bind(1, "unsupported");

        var result = fixture.Think(1);

        Assert.False(result.Success);
        Assert.Equal(BrainDecision.Empty, result.Decision);
        Assert.Contains("return type", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, fixture.Metrics.Current.HookFailures);
    }

    [Fact]
    public void Invoke_MalformedIntentTable_RemainsSuccessfulUnknownIntent()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain(
            "malformed",
            """
            return {
              id = "malformed",
              default_tick_ms = 1000,
              perception_range = 12,
              hearing_range = 15,
              think = function()
                return { text = "missing type" }
              end
            }
            """
        );
        fixture.Bind(1, "malformed");

        var result = fixture.Think(1);

        Assert.True(result.Success, result.Error);
        Assert.Equal(BrainIntentType.Unknown, Assert.Single(result.Decision.Intents).Type);
        Assert.Equal(0, fixture.Metrics.Current.HookFailures);
    }

    [Fact]
    public void TryReload_InvalidReplacement_PreservesSharedDefinition()
    {
        using var fixture = new BrainRuntimeFixture();
        fixture.WriteBrain("counter", CounterBrain);
        fixture.Bind(1, "counter");
        fixture.WriteBrain("counter", "return { id = 'counter' }");

        var reloadResult = fixture.Runtime.TryReload("counter", out var descriptor, out var error);
        var invocation = fixture.Think(1);

        Assert.False(reloadResult);
        Assert.Null(descriptor);
        Assert.NotNull(error);
        Assert.True(invocation.Success, invocation.Error);
        Assert.Equal("1", Assert.Single(invocation.Decision.Intents).Text);
        Assert.Equal(1, fixture.Metrics.Current.ReloadFallbacks);
    }

    private static void AssertHookText(
        BrainRuntimeFixture fixture,
        NpcBrainHookType hook,
        NpcBrainEvent? brainEvent,
        string expected
    )
    {
        var result = fixture.Runtime.Invoke(new(1), hook, fixture.Context, brainEvent);

        Assert.True(result.Success, result.Error);
        Assert.Equal(expected, Assert.Single(result.Decision.Intents).Text);
    }

    private sealed class BrainRuntimeFixture : IDisposable
    {
        private readonly Container _container = new();
        private readonly DirectoriesConfig _directories;
        private readonly LuaScriptEngineService _engine;
        private readonly string _root;
        private readonly string _scripts;

        public BrainContext Context { get; }

        public NpcAiMetrics Metrics { get; } = new();

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

        public BrainRuntimeFixture(int maxIntentsPerDecision = 8)
        {
            _root = Path.Combine(Path.GetTempPath(), "mg-brain-" + Guid.NewGuid().ToString("N"));
            _directories = new(_root, ["scripts"]);
            _scripts = _directories.GetPath("scripts");
            _engine = new(
                _directories,
                _container,
                new LuaEngineConfig(_root, _scripts, "MoongateTests", "1.0.0")
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
            Runtime = new(_engine, _directories, config, Metrics);
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

        public void CreateBrainSymbolicLink(string brainId, string targetContent)
        {
            var brainDirectory = Path.Combine(_scripts, "brains");
            Directory.CreateDirectory(brainDirectory);
            var targetPath = Path.Combine(_root, brainId + "-outside.lua");
            File.WriteAllText(targetPath, targetContent);
            File.CreateSymbolicLink(Path.Combine(brainDirectory, brainId + ".lua"), targetPath);
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

        public void Dispose()
        {
            _engine.Dispose();
            _container.Dispose();
            Directory.Delete(_root, true);
        }
    }
}
