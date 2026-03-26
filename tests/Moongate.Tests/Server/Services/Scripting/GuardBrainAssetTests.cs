using System.Text.Json;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class GuardBrainAssetTests
{
    [Test]
    public void GuardBrainScript_ShouldGreetPlayersOnceAndAttackEnemiesOnInRange()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "guard.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("function guard.on_in_range"));
                Assert.That(script, Does.Contain("Hello, \" .. source_name .. \", How do you feel today?"));
                Assert.That(script, Does.Contain("source_is_enemy"));
                Assert.That(script, Does.Contain("npc:set_target(source)"));
                Assert.That(script, Does.Contain("npc:set_war_mode(true)"));
                Assert.That(script, Does.Contain("combat.set_target(npc_serial, source_serial)"));
                Assert.That(script, Does.Contain("guards.set_focus"));
                Assert.That(script, Does.Contain("guards.teleport_to_target"));
                Assert.That(script, Does.Contain("local function set_default(key, value)"));
                Assert.That(script, Does.Contain("set_default(\"evade_desired_range\", 0)"));
                Assert.That(script, Does.Contain("set_default(HOME_X_KEY, npc.location_x)"));
                Assert.That(script, Does.Contain("set_default(\"hold_radius\", 1)"));
                Assert.That(script, Does.Contain("set_default(\"leash_radius\", 8)"));
                Assert.That(script, Does.Contain("\"self_bandage\""));
                Assert.That(script, Does.Contain("set_default(\"self_bandage_hp_threshold\", 0.45)"));
                Assert.That(script, Does.Contain("set_default(\"self_bandage_score_bonus\", 70)"));
                Assert.That(script, Does.Not.Contain("npc_state.set_var(npc_serial, get_seen_key(source_serial), nil)"));
                Assert.That(script, Does.Contain("if source_is_enemy then"));
                Assert.That(script, Does.Not.Contain("event_type == \"speech_heard\""));
            }
        );
    }

    [Test]
    public void GuardBrainScript_ShouldUseRangedKeepDistanceForRangedGuards()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "guard.lua");
        var script = File.ReadAllText(scriptPath);
        var behaviorInitPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "behaviors", "init.lua");
        var behaviorInit = File.ReadAllText(behaviorInitPath);
        var behaviorPath = Path.Combine(
            repositoryRoot,
            "moongate_data",
            "scripts",
            "ai",
            "behaviors",
            "ranged_keep_distance.lua"
        );
        var followBehaviorPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "behaviors", "follow.lua");
        var holdPositionPath = Path.Combine(
            repositoryRoot,
            "moongate_data",
            "scripts",
            "ai",
            "behaviors",
            "hold_position.lua"
        );
        var returnHomePath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "behaviors", "return_home.lua");
        var leashPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "behaviors", "leash.lua");
        var selfBandageBehaviorPath = Path.Combine(
            repositoryRoot,
            "moongate_data",
            "scripts",
            "ai",
            "behaviors",
            "self_bandage.lua"
        );
        var rangedKeepDistance = File.ReadAllText(behaviorPath);
        var followBehavior = File.ReadAllText(followBehaviorPath);
        var holdPosition = File.ReadAllText(holdPositionPath);
        var returnHome = File.ReadAllText(returnHomePath);
        var leash = File.ReadAllText(leashPath);
        var selfBandageBehavior = File.ReadAllText(selfBandageBehaviorPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(File.Exists(behaviorPath), Is.True);
                Assert.That(behaviorInit, Does.Contain("require(\"ai.behaviors.hold_position\")"));
                Assert.That(behaviorInit, Does.Contain("require(\"ai.behaviors.return_home\")"));
                Assert.That(behaviorInit, Does.Contain("require(\"ai.behaviors.leash\")"));
                Assert.That(behaviorInit, Does.Contain("require(\"ai.behaviors.ranged_keep_distance\")"));
                Assert.That(script, Does.Contain("\"leash\""));
                Assert.That(script, Does.Contain("\"return_home\""));
                Assert.That(script, Does.Contain("\"hold_position\""));
                Assert.That(behaviorInit, Does.Contain("require(\"ai.behaviors.self_bandage\")"));
                Assert.That(script, Does.Contain("\"ranged_keep_distance\""));
                Assert.That(script, Does.Contain("\"leash\""));
                Assert.That(script, Does.Contain("\"return_home\""));
                Assert.That(script, Does.Contain("\"hold_position\""));
                Assert.That(script, Does.Contain("guard_role"));
                Assert.That(script, Does.Contain("preferred_min_range"));
                Assert.That(script, Does.Contain("preferred_max_range"));
                Assert.That(script, Does.Contain("local function set_default(key, value)"));
                Assert.That(script, Does.Contain("combat.clear_target(npc_serial)"));
                Assert.That(rangedKeepDistance, Does.Contain("combat.set_target(npc_serial, target_serial)"));
                Assert.That(
                    rangedKeepDistance,
                    Does.Contain("if combat.set_target(npc_serial, target_serial) ~= true then")
                );
                Assert.That(rangedKeepDistance, Does.Contain("npc_state.set_var(npc_serial, FOLLOW_TARGET_KEY, nil)"));
                Assert.That(followBehavior, Does.Contain("combat.set_target(npc_serial, target_serial)"));
                Assert.That(followBehavior, Does.Contain("if combat.set_target(npc_serial, target_serial) ~= true then"));
                Assert.That(followBehavior, Does.Contain("npc_state.set_var(npc_serial, FOLLOW_TARGET_KEY, nil)"));
                Assert.That(rangedKeepDistance, Does.Not.Contain("require(\"combat\")"));
                Assert.That(followBehavior, Does.Not.Contain("require(\"combat\")"));
                Assert.That(holdPosition, Does.Contain("behavior.register(\"hold_position\", M)"));
                Assert.That(returnHome, Does.Contain("steering.move_to(npc_serial"));
                Assert.That(returnHome, Does.Contain("behavior.register(\"return_home\", M)"));
                Assert.That(leash, Does.Contain("combat.clear_target(npc_serial)"));
                Assert.That(leash, Does.Contain("behavior.register(\"leash\", M)"));
                Assert.That(selfBandageBehavior, Does.Contain("healing.begin_self_bandage(npc_serial)"));
                Assert.That(selfBandageBehavior, Does.Contain("healing.has_bandage(npc_serial)"));
                Assert.That(selfBandageBehavior, Does.Contain("healing.is_bandaging(npc_serial)"));
                Assert.That(selfBandageBehavior, Does.Contain("behavior.register(\"self_bandage\", M)"));
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
