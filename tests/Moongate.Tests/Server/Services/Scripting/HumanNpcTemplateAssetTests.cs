using System.Text.Json;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class HumanNpcTemplateAssetTests
{
    [Test]
    public void NpcsHumansTemplate_ShouldDefineCanonicalBaseHumanAiBlock()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "npcs_humans.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var baseHuman = document.RootElement
                                .EnumerateArray()
                                .First(
                                    element => string.Equals(
                                        element.GetProperty("id").GetString(),
                                        "base_human_npc",
                                        StringComparison.Ordinal
                                    )
                                );

        Assert.Multiple(
            () =>
            {
                Assert.That(baseHuman.GetProperty("ai").GetProperty("brain").GetString(), Is.EqualTo("none"));
                Assert.That(baseHuman.GetProperty("ai").GetProperty("fightMode").GetString(), Is.EqualTo("none"));
                Assert.That(baseHuman.GetProperty("ai").GetProperty("rangePerception").GetInt32(), Is.EqualTo(16));
                Assert.That(baseHuman.GetProperty("ai").GetProperty("rangeFight").GetInt32(), Is.EqualTo(1));
                Assert.That(baseHuman.TryGetProperty("brain", out _), Is.False);
            }
        );
    }

    [Test]
    public void NpcsHumansTemplate_ShouldLetGenericNpcInheritBaseHumanAiWithoutOverrides()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "npcs_humans.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var genericNpc = document.RootElement
                                 .EnumerateArray()
                                 .First(
                                     element => string.Equals(
                                         element.GetProperty("id").GetString(),
                                         "generic_npc",
                                         StringComparison.Ordinal
                                     )
                                 );

        Assert.Multiple(
            () =>
            {
                Assert.That(genericNpc.TryGetProperty("ai", out _), Is.False);
                Assert.That(genericNpc.TryGetProperty("brain", out _), Is.False);
            }
        );
    }

    [Test]
    public void NpcsHumansTemplate_ShouldOverrideHealerBrainWithoutRepeatingInheritedDefaults()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "npcs_humans.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var healer = document.RootElement
                             .EnumerateArray()
                             .First(
                                 element => string.Equals(
                                     element.GetProperty("id").GetString(),
                                     "healer_npc",
                                     StringComparison.Ordinal
                                 )
                             );

        var ai = healer.GetProperty("ai");

        Assert.Multiple(
            () =>
            {
                Assert.That(ai.GetProperty("brain").GetString(), Is.EqualTo("town_healer"));
                Assert.That(ai.TryGetProperty("fightMode", out _), Is.False);
                Assert.That(ai.TryGetProperty("rangePerception", out _), Is.False);
                Assert.That(ai.TryGetProperty("rangeFight", out _), Is.False);
                Assert.That(healer.TryGetProperty("brain", out _), Is.False);
            }
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
