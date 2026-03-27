using System.Text.Json;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class TownBankerBrainAssetTests
{
    [Test]
    public void InitScript_ShouldRequireTownBankerBrain()
    {
        var repositoryRoot = GetRepositoryRoot();
        var initScriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "init.lua");
        var initScript = File.ReadAllText(initScriptPath);

        Assert.That(initScript, Does.Contain("require(\"ai.init\")"));
    }

    [Test]
    public void NpcsHumansTemplate_ShouldAssignTownBankerBrainToBankerNpc()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "townfolk", "banker_npc.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var banker = document.RootElement
                             .EnumerateArray()
                             .First(
                                 element => string.Equals(
                                     element.GetProperty("id").GetString(),
                                     "banker_npc",
                                     StringComparison.Ordinal
                                 )
                             );

        var ai = banker.GetProperty("ai");

        Assert.That(ai.GetProperty("brain").GetString(), Is.EqualTo("town_banker"));
        Assert.That(ai.GetProperty("fightMode").GetString(), Is.EqualTo("none"));
        Assert.That(ai.GetProperty("rangePerception").GetInt32(), Is.EqualTo(2));
        Assert.That(ai.GetProperty("rangeFight").GetInt32(), Is.EqualTo(1));
        Assert.That(banker.TryGetProperty("brain", out _), Is.False);
    }

    [Test]
    public void TownBankerBrain_ShouldExposeOpenBankContextMenu()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "town_banker.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("key = \"open_bank\""));
                Assert.That(script, Does.Contain("cliloc.open_bank"));
                Assert.That(script, Does.Contain("bank.open(session_id)"));
            }
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
