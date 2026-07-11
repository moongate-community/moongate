using Moongate.UO.Data.Loot;
using Moongate.UO.Data.Types;
using SquidStd.Core.Yaml;

namespace Moongate.Tests.Data;

public class LootTemplateYamlTests
{
    private const string Sample =
        "- Id: weapon_pack\n" +
        "  Name: Weapon pack\n" +
        "  Category: Test\n" +
        "  Description: Weighted sample.\n" +
        "  Mode: Weighted\n" +
        "  Rolls: 2\n" +
        "  NoDropWeight: 3\n" +
        "  Entries:\n" +
        "  - ItemTemplateId: broadsword\n" +
        "    Weight: 4\n" +
        "    Amount: 1\n" +
        "- Id: reagent_pack\n" +
        "  Name: Reagent pack\n" +
        "  Category: Test\n" +
        "  Description: Additive sample.\n" +
        "  Mode: Additive\n" +
        "  Rolls: 1\n" +
        "  NoDropWeight: 0\n" +
        "  Entries:\n" +
        "  - ItemTag: reagent\n" +
        "    Chance: 0.5\n" +
        "    AmountMin: 2\n" +
        "    AmountMax: 5\n";

    [Fact]
    public void Deserialize_BindsWeightedAndAdditiveEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), "loot-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, Sample);

        try
        {
            var templates = YamlUtils.DeserializeFromFile<LootTemplate[]>(path)!;

            Assert.Equal(2, templates.Length);
            Assert.Equal(LootTemplateModeType.Weighted, templates[0].Mode);
            Assert.Equal(2, templates[0].Rolls);
            Assert.Equal(3, templates[0].NoDropWeight);
            Assert.Equal("broadsword", templates[0].Entries.Single().ItemTemplateId);
            Assert.Equal(4, templates[0].Entries.Single().Weight);
            Assert.Equal(1, templates[0].Entries.Single().Amount);
            Assert.Null(templates[0].Entries.Single().Chance);

            Assert.Equal(LootTemplateModeType.Additive, templates[1].Mode);
            Assert.Equal("reagent", templates[1].Entries.Single().ItemTag);
            Assert.Equal(0.5, templates[1].Entries.Single().Chance);
            Assert.Equal(2, templates[1].Entries.Single().AmountMin);
            Assert.Equal(5, templates[1].Entries.Single().AmountMax);
            Assert.Null(templates[1].Entries.Single().Weight);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
