using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;
using SquidStd.Core.Yaml;

namespace Moongate.Tests.UO.Data.Skills;

public class SkillDefinitionYamlTests
{
    [Fact]
    public void DeserializeFromFile_ReadsPascalCaseKeysAndStatEnum()
    {
        var yaml =
            "- Id: 0\n" +
            "  Name: Alchemy\n" +
            "  Title: Alchemist\n" +
            "  StrScale: 0\n" +
            "  DexScale: 0.05\n" +
            "  IntScale: 0.05\n" +
            "  StrGain: 0\n" +
            "  DexGain: 0.5\n" +
            "  IntGain: 0.5\n" +
            "  GainFactor: 1\n" +
            "  PrimaryStat: Int\n" +
            "  SecondaryStat: Dex\n";

        var path = Path.Combine(Path.GetTempPath(), "mg-skilldef-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, yaml);

        try
        {
            var defs = YamlUtils.DeserializeFromFile<SkillDefinition[]>(path);

            Assert.NotNull(defs);
            var def = Assert.Single(defs!);
            Assert.Equal(0, def.Id);
            Assert.Equal("Alchemy", def.Name);
            Assert.Equal("Alchemist", def.Title);
            Assert.Equal(0.05, def.DexScale, 5);
            Assert.Equal(0.05, def.IntScale, 5);
            Assert.Equal(0.5, def.DexGain, 5);
            Assert.Equal(1, def.GainFactor, 5);
            Assert.Equal(Stat.Int, def.PrimaryStat);
            Assert.Equal(Stat.Dex, def.SecondaryStat);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
