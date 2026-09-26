using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;
using Moongate.UoxItemConverter.Internal;
using Moongate.UoxItemConverter.Tests.TestSupport;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class UoxMobileConverterTests : IDisposable
{
    private readonly ConverterTestDirectories _dirs = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string CombinedOutput => _output + _error.ToString();

    public UoxMobileConverterTests()
    {
        AppContext.SetSwitch("Tomlyn.TomlSerializer.IsReflectionEnabledByDefault", true);
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
    }

    [Fact]
    public void Run_TheNameLists_AreWrittenWithReadableIdsAndDictionaryNames()
    {
        _dirs.WriteSource("items.dfn", "[0x0eed]\n{\nid=0x0eed\n}\n");
        _dirs.WriteMobileSource(
            "npc/namelists.dfn",
            "[RANDOMNAME 1]\n{ Basic Human Male Names\nAaron\nAbbott\nAaron\n}\n[RANDOMNAME 5]\n{\n3009//a daemon\nImp\n}\n"
        );
        _dirs.WriteMobileSource("../dictionaries/dictionary.ENG", "3009=a daemon\n");

        Assert.True(Run() == 0, CombinedOutput);

        var names = TomlUtils.DeserializeFromFile<NameListFile>(_dirs.NamesDestinationPath)!.Names;
        Assert.Equal(["Aaron", "Abbott"], names.Single(list => list.Id == "male").Names);
        Assert.Equal(["a daemon", "Imp"], names.Single(list => list.Id == "daemon").Names);
    }

    [Fact]
    public void Run_InheritanceFollowsGetAndTheLbrEra()
    {
        WriteItemsAndNames();
        _dirs.WriteMobileSource(
            "npc/orcs.dfn",
            """
            [base_orc]
            {
            NAME=#//an orc
            ID=0x0011
            }
            [orc_lbr]
            {
            GET=base_orc
            TITLE=5052//the Blacksmith
            }
            [orc_aos]
            {
            GET=base_orc
            }
            [orc]
            {
            GETAOS=orc_aos
            GETLBR=orc_lbr
            }
            """
        );
        _dirs.WriteMobileSource("../dictionaries/dictionary.ENG", "5052=the Blacksmith\n");

        Assert.True(Run() == 0, CombinedOutput);

        var mobiles = ReadMobiles("orcs.toml");
        Assert.Equal(("an orc", (int?)0x11), (mobiles["base_orc"].Name, mobiles["base_orc"].Body));
        Assert.Equal("base_orc", mobiles["orc_lbr"].BaseId);
        Assert.Equal("the Blacksmith", mobiles["orc_lbr"].Title);
        Assert.Equal("orc_lbr", mobiles["orc"].BaseId);
    }

    [Theory,
     InlineData("ID=0x0190", RaceType.Human, MobileGenderType.Male, null),
     InlineData("ID=0x0191", RaceType.Human, MobileGenderType.Female, null),
     InlineData("ID=0x025E", RaceType.Elf, MobileGenderType.Female, null),
     InlineData("ID=0x0033\nRACE=22", null, null, 0x33)]
    public void Run_HumanoidBodiesBecomeRaceAndGender(string lines, RaceType? race, MobileGenderType? gender, int? body)
    {
        WriteItemsAndNames();
        _dirs.WriteMobileSource("npc/a.dfn", $"[x]\n{{\n{lines}\nNAMELIST=2\n}}\n");

        Assert.True(Run() == 0, CombinedOutput);

        var x = ReadMobiles("a.toml")["x"];
        Assert.Equal((race, gender, body, "female"), (x.Race, x.Gender, x.Body, x.NameList));
    }

    private int Run()
    {
        return UoxItemConverterCommand.Run(
            _dirs.SourceDirectory,
            _dirs.DestinationDirectory,
            _dirs.LootDestinationDirectory,
            _output,
            _error,
            _dirs.MobileSourceDirectory,
            _dirs.MobileDestinationDirectory,
            _dirs.NamesDestinationPath
        );
    }

    private void WriteItemsAndNames()
    {
        _dirs.WriteSource("items.dfn", "[0x0eed]\n{\nid=0x0eed\n}\n");
        WriteNames();
    }

    private void WriteNames()
    {
        _dirs.WriteMobileSource("npc/namelists.dfn", "[RANDOMNAME 1]\n{\nAaron\n}\n[RANDOMNAME 2]\n{\nAba\n}\n");
    }

    private Dictionary<string, MobileTemplate> ReadMobiles(string relativePath)
    {
        return TomlUtils.DeserializeFromFile<MobileTemplateFile>(Path.Combine(_dirs.MobileDestinationDirectory, relativePath))!
                        .Mobile.ToDictionary(mobile => mobile.Id);
    }

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        _dirs.Dispose();
    }
}
