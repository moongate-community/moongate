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
            Assert.That(guard.GetProperty("brain").GetString(), Is.EqualTo("guard"));
        }
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
