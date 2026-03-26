using System.Text.Json;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class MountAndTestMobAssetTests
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
                Assert.That(
                    etherealHorseMount.GetProperty("params").GetProperty("mounted_display_item_id").GetProperty("value").GetString(),
                    Is.EqualTo("0x3EA0")
                );
            }
        );
    }

    [Test]
    public void TestMobTemplate_ShouldPreserveCustomIdsAndBrainAssignments()
    {
        var repositoryRoot = GetRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles", "test_mob.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var orione = document.RootElement
                              .EnumerateArray()
                              .First(
                                  element => string.Equals(
                                      element.GetProperty("id").GetString(),
                                      "orione",
                                      StringComparison.Ordinal
                                  )
                              );
        var vega = document.RootElement
                           .EnumerateArray()
                           .First(
                               element => string.Equals(
                                   element.GetProperty("id").GetString(),
                                   "vega",
                                   StringComparison.Ordinal
                               )
                           );

        Assert.Multiple(
            () =>
            {
                Assert.That(orione.GetProperty("ai").GetProperty("brain").GetString(), Is.EqualTo("orion"));
                Assert.That(orione.GetProperty("ai").GetProperty("fightMode").GetString(), Is.EqualTo("none"));
                Assert.That(orione.GetProperty("ai").GetProperty("rangePerception").GetInt32(), Is.EqualTo(16));
                Assert.That(orione.GetProperty("ai").GetProperty("rangeFight").GetInt32(), Is.EqualTo(1));

                Assert.That(vega.GetProperty("ai").GetProperty("brain").GetString(), Is.EqualTo("vega"));
                Assert.That(vega.GetProperty("ai").GetProperty("fightMode").GetString(), Is.EqualTo("none"));
                Assert.That(vega.GetProperty("ai").GetProperty("rangePerception").GetInt32(), Is.EqualTo(16));
                Assert.That(vega.GetProperty("ai").GetProperty("rangeFight").GetInt32(), Is.EqualTo(1));
            }
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
