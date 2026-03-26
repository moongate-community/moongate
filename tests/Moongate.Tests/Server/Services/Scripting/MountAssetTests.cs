using System.Text.Json;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class MountAssetTests
{
    [Test]
    public void MountsTemplate_ShouldPreserveEtherealHorseMountAiBlock()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "mounts.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var etherealHorseMount = document.RootElement
                                         .EnumerateArray()
                                         .First(
                                             element => string.Equals(
                                                 element.GetProperty("id").GetString(),
                                                 "ethereal_horse_mount",
                                                 StringComparison.Ordinal
                                             )
                                         );

        var ai = etherealHorseMount.GetProperty("ai");

        Assert.Multiple(
            () =>
            {
                Assert.That(ai.GetProperty("brain").GetString(), Is.EqualTo("none"));
                Assert.That(ai.GetProperty("fightMode").GetString(), Is.EqualTo("none"));
                Assert.That(ai.GetProperty("rangePerception").GetInt32(), Is.EqualTo(16));
                Assert.That(ai.GetProperty("rangeFight").GetInt32(), Is.EqualTo(1));
                Assert.That(etherealHorseMount.TryGetProperty("brain", out _), Is.False);
                Assert.That(
                    etherealHorseMount.GetProperty("params").GetProperty("mounted_display_item_id").GetProperty("value").GetString(),
                    Is.EqualTo("0x3EA0")
                );
            }
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
