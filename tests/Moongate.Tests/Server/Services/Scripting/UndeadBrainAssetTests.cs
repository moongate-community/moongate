using System.Text.Json;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class UndeadBrainAssetTests
{
    [Test]
    public void UndeadMeleeBrainScript_ShouldTickEveryTwoSecondsAndAcquireNearestEnemy()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "undead_melee.lua");

        Assert.That(File.Exists(scriptPath), Is.True);

        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("undead_melee = {}"));
                Assert.That(script, Does.Contain("perception.find_nearest_enemy(npc_serial"));
                Assert.That(script, Does.Contain("npc:set_target(target)"));
                Assert.That(script, Does.Contain("npc:set_war_mode(true)"));
                Assert.That(script, Does.Contain("combat.set_target(npc_serial, target_serial)"));
                Assert.That(script, Does.Contain("combat.clear_target(npc_serial)"));
                Assert.That(script, Does.Contain("coroutine.yield(2000)"));
            }
        );
    }

    [Test]
    public void UndeadTemplates_ShouldAssignUndeadMeleeBrainToZombieNpc()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "undead.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var zombie = document.RootElement
                             .EnumerateArray()
                             .First(element => string.Equals(element.GetProperty("id").GetString(), "zombie_npc", StringComparison.Ordinal));

        Assert.That(zombie.GetProperty("brain").GetString(), Is.EqualTo("undead_melee"));
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
