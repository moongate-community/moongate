using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Types.Guilds;
using Moongate.UoxItemConverter.Internal;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class ModernUoGuildmasterConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "moongate-modernuo-guildmasters-" + Guid.NewGuid().ToString("N")
    );

    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string Source => Path.Combine(_root, "Guildmasters");

    private string Mobiles => Path.Combine(_root, "mobiles");

    private string Lists => Path.Combine(_root, "npc_lists");

    private string CombinedOutput => _output + _error.ToString();

    [Fact]
    public void Run_AGuildmaster_BecomesAManAndAWoman_WithTheSkillsTheTitleAndTheGuild()
    {
        Write(
            "BlacksmithGuildmaster.cs",
            """
            public partial class BlacksmithGuildmaster : BaseGuildmaster
            {
                public BlacksmithGuildmaster() : base("blacksmith")
                {
                    SetSkill(SkillName.ArmsLore, 65.0, 88.0);
                    SetSkill(SkillName.Blacksmith, 90.0, 100.0);
                    SetSkill(SkillName.Macing, 36.0, 68.0);
                    SetSkill(SkillName.Parry, 36.0, 68.0);
                }

                public override NpcGuild NpcGuild => NpcGuild.BlacksmithsGuild;
            }
            """
        );
        Write("BaseGuildmaster.cs", "public abstract class BaseGuildmaster { }");

        Assert.True(Run() == 0, CombinedOutput);

        var masters = TomlUtils.DeserializeFromFile<MobileTemplateFile>(Path.Combine(Mobiles, "guildmasters.toml"))!.Mobile;
        Assert.Equal(["m_blacksmith_guildmaster", "f_blacksmith_guildmaster"], masters.Select(master => master.Id));
        var man = masters[0];
        Assert.Equal(
            ("basevendor", "the blacksmith guildmaster", NpcGuildType.Blacksmiths),
            (man.BaseId, man.Title, man.NpcGuild)
        );
        Assert.Equal("the blacksmith guildmistress", masters[1].Title);
        Assert.Equal(
            ["arms_lore", "blacksmithy", "mace_fighting", "parrying"],
            man.Skills!.Keys.Order(StringComparer.Ordinal)
        );
        Assert.Equal(11, man.Skills["blacksmithy"].Max - man.Skills["blacksmithy"].Min + 1);
        Assert.Equal(90, man.Skills["blacksmithy"].Min);

        var list = Assert.Single(
            TomlUtils.DeserializeFromFile<NpcListTemplateFile>(Path.Combine(Lists, "npclists_guildmasters.toml"))!.NpcList
        );
        Assert.Equal("blacksmithguildmaster", list.Id);
        Assert.Equal(["m_blacksmith_guildmaster", "f_blacksmith_guildmaster"], list.Entries.Select(entry => entry.MobileId));
    }

    [Fact]
    public void Run_AGuildmasterThatNamesNoGuild_TeachesOnly_AndTheReportSaysSo()
    {
        Write(
            "MinerGuildmaster.cs",
            """
            public partial class MinerGuildmaster : BaseGuildmaster
            {
                public MinerGuildmaster() : base("miner")
                {
                    SetSkill(SkillName.Mining, 90.0, 100.0);
                }
            }
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var masters = TomlUtils.DeserializeFromFile<MobileTemplateFile>(Path.Combine(Mobiles, "guildmasters.toml"))!.Mobile;
        Assert.All(masters, master => Assert.Null(master.NpcGuild));
        Assert.Contains("names no guild", _output.ToString());
    }

    [Fact]
    public void Run_AFolderWithNoGuildmaster_ExitsWithAnError()
    {
        Directory.CreateDirectory(Source);

        Assert.Equal(2, Run());
        Assert.Contains("no guildmaster", _error.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private void Write(string name, string text)
    {
        Directory.CreateDirectory(Source);
        File.WriteAllText(Path.Combine(Source, name), text);
    }

    private int Run()
    {
        return ModernUoGuildmasterConverter.Run(Source, Mobiles, Lists, _output, _error);
    }
}
