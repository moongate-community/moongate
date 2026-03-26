using System.Text.Json;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class GuardBrainAssetTests
{
    [Test]
    public void GuardBrainScript_ShouldUseAiRuntimeHelpers()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "guard.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("require(\"ai.runtime.fsm\")"));
                Assert.That(script, Does.Contain("require(\"ai.runtime.movement\")"));
                Assert.That(script, Does.Contain("require(\"ai.runtime.targeting\")"));
                Assert.That(script, Does.Not.Contain("ai.modernuo."));
            }
        );
    }

    [Test]
    public void GuardBrainScript_ShouldSupportOptionalRandomRoamPatrolMode()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "guard.lua");
        var script = File.ReadAllText(scriptPath);
        var onThinkStart = script.IndexOf("function guard.on_think", StringComparison.Ordinal);
        var onEventStart = script.IndexOf("function guard.on_event", StringComparison.Ordinal);

        Assert.That(onThinkStart, Is.GreaterThanOrEqualTo(0));
        Assert.That(onEventStart, Is.GreaterThan(onThinkStart));

        var onThinkScript = script.Substring(onThinkStart, onEventStart - onThinkStart);

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    onThinkScript,
                    Does.Match(@"patrol_mode\s*==\s*""random_roam""[\s\S]*(?:movement|steering)\.wander\([^)]*\bpatrol_radius\b[^)]*\)")
                );
                Assert.That(onThinkScript, Does.Contain("movement.guard(npc_serial)"));
                Assert.That(onThinkScript, Does.Contain("should_return_home(npc_serial, npc)"));
                Assert.That(onThinkScript, Does.Contain("move_home(npc_serial, npc)"));
            }
        );
    }

    [Test]
    public void GuardBrainScript_ShouldGreetPlayersOnceAndAttackEnemiesOnInRange()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "guard.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("local guards = guards"));
                Assert.That(script, Does.Not.Contain("require(\"guards\")"));
                Assert.That(script, Does.Contain("function guard.on_think"));
                Assert.That(script, Does.Contain("function guard.on_in_range"));
                Assert.That(script, Does.Contain("function guard.on_out_range"));
                Assert.That(script, Does.Contain("function guard.on_death"));
                Assert.That(script, Does.Contain("Hello, \" .. source_name .. \", How do you feel today?"));
                Assert.That(script, Does.Contain("source_is_enemy"));
                Assert.That(script, Does.Contain("npc:set_target(source)"));
                Assert.That(script, Does.Contain("npc:set_war_mode(true)"));
                Assert.That(script, Does.Contain("combat.set_target(npc_serial, source_serial)"));
                Assert.That(script, Does.Contain("guards.set_focus"));
                Assert.That(script, Does.Contain("guards.get_focus"));
                Assert.That(script, Does.Contain("guards.teleport_to_target"));
                Assert.That(script, Does.Contain("guards.try_reveal"));
                Assert.That(script, Does.Contain("guard_role"));
                Assert.That(script, Does.Contain("guard_mode"));
                Assert.That(script, Does.Contain("mobile.get_ai_range_perception"));
                Assert.That(script, Does.Contain("mobile.get_ai_range_fight"));
                Assert.That(script, Does.Contain("targeting.find_hostile_target"));
                Assert.That(script, Does.Contain("fsm.set_target"));
                Assert.That(script, Does.Contain("fsm.clear_target"));
                Assert.That(script, Does.Not.Contain("utility_runner"));
            }
        );
    }

    [Test]
    public void GuardBrainScript_ShouldBranchOnGuardRoleAndRoleSpecificRanges()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "guard.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("local function is_ranged_guard(npc_serial)"));
                Assert.That(script, Does.Contain("guard_role"));
                Assert.That(script, Does.Contain("local function handle_melee_guard"));
                Assert.That(script, Does.Contain("local function handle_ranged_guard"));
                Assert.That(script, Does.Contain("preferred_min_range"));
                Assert.That(script, Does.Contain("preferred_max_range"));
                Assert.That(script, Does.Contain("set_default(npc_serial, PREFERRED_MIN_RANGE_KEY, ranged_guard and 4 or 1)"));
                Assert.That(script, Does.Contain("set_default(npc_serial, PREFERRED_MAX_RANGE_KEY, ranged_guard and 6 or 1)"));
                Assert.That(script, Does.Contain("set_default(npc_serial, GUARD_MODE_KEY, ranged_guard and \"ranged\" or \"melee\")"));
                Assert.That(script, Does.Contain("should_return_home"));
                Assert.That(script, Does.Contain("move_home(npc_serial, npc)"));
                Assert.That(script, Does.Contain("guards.try_reveal"));
                Assert.That(script, Does.Contain("guards.teleport_to_target"));
                Assert.That(script, Does.Contain("set_focus(npc_serial, target_serial)"));
                Assert.That(script, Does.Contain("clear_focus(npc_serial, npc)"));
                Assert.That(script, Does.Not.Contain("utility_runner"));
                Assert.That(script, Does.Not.Contain("behavior.register(\"hold_position\""));
                Assert.That(script, Does.Not.Contain("\"ranged_keep_distance\""));
            }
        );
    }

    [Test]
    public void GuardBrainScript_ShouldPreserveFocusCleanupOnOutRange()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "guard.lua");
        var script = File.ReadAllText(scriptPath);
        var onOutRangeStart = script.IndexOf("function guard.on_out_range", StringComparison.Ordinal);
        var onDeathStart = script.IndexOf("function guard.on_death", StringComparison.Ordinal);

        Assert.That(onOutRangeStart, Is.GreaterThanOrEqualTo(0));
        Assert.That(onDeathStart, Is.GreaterThan(onOutRangeStart));

        var onOutRangeScript = script.Substring(onOutRangeStart, onDeathStart - onOutRangeStart);

        Assert.Multiple(
            () =>
            {
                Assert.That(onOutRangeScript, Does.Contain("npc_state.set_var(npc_serial, get_engaged_key(source_serial), nil)"));
                Assert.That(onOutRangeScript, Does.Not.Contain("clear_focus(npc_serial, npc)"));
                Assert.That(onOutRangeScript, Does.Not.Contain("guards.set_focus(npc_serial, nil)"));
                Assert.That(onOutRangeScript, Does.Not.Contain("fsm.clear_target(npc_serial)"));
                Assert.That(onOutRangeScript, Does.Not.Contain("combat.clear_target(npc_serial)"));
            }
        );
    }

    [Test]
    public void GuardBrainScript_ShouldRefreshFocusFromCombatHooksWithoutInRangePayloadFields()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "guard.lua");
        var script = File.ReadAllText(scriptPath);
        var combatHelperStart = script.IndexOf("local function handle_combat_hook", StringComparison.Ordinal);
        var combatHelperEnd = script.IndexOf("function guard.on_think", StringComparison.Ordinal);
        var eventHandlerStart = script.IndexOf("function guard.on_event", StringComparison.Ordinal);
        var inRangeStart = script.IndexOf("function guard.on_in_range", StringComparison.Ordinal);

        Assert.That(combatHelperStart, Is.GreaterThanOrEqualTo(0));
        Assert.That(combatHelperEnd, Is.GreaterThan(combatHelperStart));
        Assert.That(eventHandlerStart, Is.GreaterThanOrEqualTo(0));
        Assert.That(inRangeStart, Is.GreaterThan(eventHandlerStart));

        var combatHelperScript = script.Substring(combatHelperStart, combatHelperEnd - combatHelperStart);
        var eventHandlerScript = script.Substring(eventHandlerStart, inRangeStart - eventHandlerStart);

        Assert.Multiple(
            () =>
            {
                Assert.That(combatHelperScript, Does.Contain("guards.try_reveal(npc_serial, source_serial)"));
                Assert.That(combatHelperScript, Does.Contain("set_focus(npc_serial, source_serial)"));
                Assert.That(combatHelperScript, Does.Contain("npc:set_target(source)"));
                Assert.That(combatHelperScript, Does.Contain("combat.set_target(npc_serial, source_serial)"));
                Assert.That(combatHelperScript, Does.Not.Contain("source_is_enemy"));
                Assert.That(combatHelperScript, Does.Not.Contain("source_name"));
                Assert.That(eventHandlerScript, Does.Contain("event_type == \"attack\""));
                Assert.That(eventHandlerScript, Does.Contain("event_type == \"missed_attack\""));
                Assert.That(eventHandlerScript, Does.Contain("event_type == \"attacked\""));
                Assert.That(eventHandlerScript, Does.Contain("event_type == \"missed_by_attack\""));
                Assert.That(eventHandlerScript, Does.Contain("handle_combat_hook(npc_serial, from_serial, event_obj)"));
            }
        );
    }

    [Test]
    public void GuardBrainScript_ShouldUseHomeMapForRecoveryAndBlockCrossMapTargets()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "guard.lua");
        var script = File.ReadAllText(scriptPath);
        var moveHomeStart = script.IndexOf("local function move_home", StringComparison.Ordinal);
        var shouldReturnHomeStart = script.IndexOf("local function should_return_home", StringComparison.Ordinal);
        var handleTargetStart = script.IndexOf("local function handle_target", StringComparison.Ordinal);
        var combatHelperStart = script.IndexOf("local function handle_combat_hook", StringComparison.Ordinal);

        Assert.That(moveHomeStart, Is.GreaterThanOrEqualTo(0));
        Assert.That(shouldReturnHomeStart, Is.GreaterThan(moveHomeStart));
        Assert.That(handleTargetStart, Is.GreaterThan(shouldReturnHomeStart));
        Assert.That(combatHelperStart, Is.GreaterThan(handleTargetStart));

        var moveHomeScript = script.Substring(moveHomeStart, shouldReturnHomeStart - moveHomeStart);
        var handleTargetScript = script.Substring(handleTargetStart, combatHelperStart - handleTargetStart);
        var shouldReturnHomeScript = script.Substring(shouldReturnHomeStart, handleTargetStart - shouldReturnHomeStart);

        Assert.Multiple(
            () =>
            {
                Assert.That(moveHomeScript, Does.Contain("home_map_id"));
                Assert.That(moveHomeScript, Does.Contain("mobile.teleport(npc_serial, home_map_id"));
                Assert.That(moveHomeScript, Does.Contain("steering.move_to(npc_serial"));
                Assert.That(shouldReturnHomeScript, Does.Contain("home_map_id ~= nil and npc.map_id ~= home_map_id"));
                Assert.That(shouldReturnHomeScript, Does.Contain("return true"));
                Assert.That(handleTargetScript, Does.Contain("target.map_id ~= npc.map_id"));
                Assert.That(handleTargetScript, Does.Contain("clear_focus(npc_serial, npc)"));
                Assert.That(handleTargetScript, Does.Contain("return move_home(npc_serial, npc)"));
            }
        );
    }

    [Test]
    public void GuardsTemplate_ShouldAssignGuardBrainToGuardNpcs()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "guards.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));

        foreach (var guard in document.RootElement.EnumerateArray())
        {
            var ai = guard.GetProperty("ai");

            Assert.That(ai.GetProperty("brain").GetString(), Is.EqualTo("guard"));
            Assert.That(ai.GetProperty("fightMode").GetString(), Is.EqualTo("aggressor"));
            Assert.That(guard.TryGetProperty("brain", out _), Is.False);
            Assert.That(guard.GetProperty("defaultFactionId").GetString(), Is.EqualTo("true_britannians"));
        }
    }

    [Test]
    public void GuardsTemplate_ShouldMarkArcherGuardsAsRanged()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "guards.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var archerIds = new[] { "archer_guard_male_npc", "archer_guard_female_npc" };

        foreach (var archerId in archerIds)
        {
            var archer = document.RootElement
                                 .EnumerateArray()
                                 .First(
                                     element => string.Equals(
                                         element.GetProperty("id").GetString(),
                                         archerId,
                                         StringComparison.Ordinal
                                     )
                                 );
            var ai = archer.GetProperty("ai");

            Assert.Multiple(
                () =>
                {
                    if (ai.TryGetProperty("rangePerception", out var rangePerception))
                    {
                        Assert.That(rangePerception.GetInt32(), Is.EqualTo(10));
                    }
                    else
                    {
                        Assert.Fail("Expected ai.rangePerception to exist for archer guards.");
                    }

                    Assert.That(ai.GetProperty("rangeFight").GetInt32(), Is.EqualTo(1));

                    Assert.That(
                        archer.GetProperty("lootTables").EnumerateArray().Select(element => element.GetString()),
                        Is.EqualTo(new[] { "guard.archer" })
                    );
                }
            );
        }
    }

    [Test]
    public void GuardsTemplate_ShouldAssignWarriorGuardLootTables()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "guards.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var warriorIds = new[] { "warrior_guard_male_npc", "warrior_guard_female_npc" };

        foreach (var warriorId in warriorIds)
        {
            var warrior = document.RootElement
                                  .EnumerateArray()
                                  .First(
                                      element => string.Equals(
                                          element.GetProperty("id").GetString(),
                                         warriorId,
                                         StringComparison.Ordinal
                                     )
                                 );
            var ai = warrior.GetProperty("ai");

            Assert.Multiple(
                () =>
                {
                    if (ai.TryGetProperty("rangePerception", out var rangePerception))
                    {
                        Assert.That(rangePerception.GetInt32(), Is.EqualTo(3));
                    }
                    else
                    {
                        Assert.Fail("Expected ai.rangePerception to exist for warrior guards.");
                    }

                    Assert.That(ai.GetProperty("rangeFight").GetInt32(), Is.EqualTo(1));

                    Assert.That(
                        warrior.GetProperty("lootTables").EnumerateArray().Select(element => element.GetString()),
                        Is.EqualTo(new[] { "guard.warrior" })
                    );
                }
            );
        }
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
