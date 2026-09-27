using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.StartingItems;
using Moongate.Ultima.Types;
using Moongate.UoxItemConverter.Internal;
using Moongate.UoxItemConverter.Tests.TestSupport;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class UoxStartingItemsConverterTests : IDisposable
{
    private readonly ConverterTestDirectories _dirs = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string CombinedOutput => _output + _error.ToString();

    public UoxStartingItemsConverterTests()
    {
        AppContext.SetSwitch("Tomlyn.TomlSerializer.IsReflectionEnabledByDefault", true);
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
    }

    [Fact]
    public void Run_BestSkillSections_BecomeSkillSetsWithPackAndEquipEntries()
    {
        WriteSources(
            """
            // Alchemy.
            [BESTSKILL 0]
            {
            PACKITEM=0x0f7a,3
            //PACKITEM=0x0f84
            EQUIPITEM=0x1f03,0x4ca
            }
            // Leave Empty
            [BESTSKILL X]
            {
            }
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var set = Assert.Single(ReadSets());
        Assert.Equal((SkillType?)SkillType.Alchemy, set.Skill);
        Assert.Equal((null, null), (set.Race, set.Gender));
        Assert.Equal(2, set.Items.Count);
        Assert.Equal(["0x0f7a_black_pearl"], set.Items[0].Items);
        Assert.Equal(("3", false), (set.Items[0].Amount!.Value.ToString(), set.Items[0].Equip));
        Assert.Equal(["0x1f03_robe"], set.Items[1].Items);
        Assert.Equal((HueSpec.FromValue(0x4ca), true), (set.Items[1].Hue!.Value, set.Items[1].Equip));
        Assert.Null(set.Items[1].Amount);
    }

    [Theory,
     InlineData("DEFAULT ALL", null, null),
     InlineData("DEFAULT MALE", RaceType.Human, GenderType.Male),
     InlineData("DEFAULT FEMALE", RaceType.Human, GenderType.Female),
     InlineData("DEFAULT ELF FEMALE", RaceType.Elf, GenderType.Female),
     InlineData("DEFAULT GARG MALE", RaceType.Gargoyle, GenderType.Male)]
    public void Run_DefaultSections_BecomeRaceAndGenderFilters(string header, RaceType? race, GenderType? gender)
    {
        WriteSources($"[{header}]\n{{\nEQUIPITEM=0x1f03\n}}\n");

        Assert.True(Run() == 0, CombinedOutput);

        var set = Assert.Single(ReadSets());
        Assert.Equal((null, race, gender), (set.Skill, set.Race, set.Gender));
    }

    [Fact]
    public void Run_ListObjectsAliasesAndTheNewbieFlag_Resolve()
    {
        WriteSources(
            """
            [BESTSKILL 25]
            {
            PACKITEM=listobject6,2
            PACKITEM=bagofreagents,1,0
            }
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var set = Assert.Single(ReadSets());
        Assert.Equal((SkillType?)SkillType.Magery, set.Skill);
        Assert.Equal(["0x0f7a_black_pearl", "0x1f03_robe"], set.Items[0].Items);
        Assert.Equal("2", set.Items[0].Amount!.Value.ToString());
        Assert.Null(set.Items[0].Newbie);
        Assert.Equal(["bagofreagents"], set.Items[1].Items);
        Assert.Equal((bool?)false, set.Items[1].Newbie);
    }

    [Fact]
    public void Run_AnUnresolvedItem_IsDroppedAndCounted()
    {
        WriteSources("[DEFAULT ALL]\n{\nPACKITEM=0x0f7a\nPACKITEM=no_such_item\n}\n");

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal(["0x0f7a_black_pearl"], Assert.Single(Assert.Single(ReadSets()).Items).Items);
        Assert.Contains("1 x unresolved item", _output.ToString());
    }

    [Fact]
    public void Run_StartingItemsWithoutMobileSource_IsRejected()
    {
        _dirs.WriteSource("items.dfn", "[0x0f7a]\n{\nid=0x0f7a\n}\n");

        var exitCode = UoxItemConverterCommand.Run(
            _dirs.SourceDirectory,
            _dirs.DestinationDirectory,
            null,
            _output,
            _error,
            startingItemsDestination: _dirs.StartingItemsDestinationPath
        );

        Assert.Equal(2, exitCode);
        Assert.Contains("--starting-items-destination needs --mobile-source", _error.ToString());
    }

    public void Dispose()
    {
        _dirs.Dispose();
    }

    private void WriteSources(string newbie)
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [0x0f7a]
            {
            id=0x0f7a
            name=black pearl
            }
            [black_pearl_alias]
            {
            get=0x0f7a
            }
            [0x1f03]
            {
            id=0x1f03
            name=robe
            }
            [bagofreagents]
            {
            id=0x0e76
            name=bag of reagents
            }
            [ITEMLIST 6]
            {
            black_pearl_alias
            2|0x1f03
            blank
            }
            """
        );
        _dirs.WriteMobileSource("npc/namelists.dfn", "[RANDOMNAME 1]\n{\nAaron\n}\n");
        _dirs.WriteMobileSource("newbie/newbie.dfn", newbie);
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
            _dirs.NamesDestinationPath,
            _dirs.StartingItemsDestinationPath
        );
    }

    private List<StartingItemSet> ReadSets()
    {
        return TomlUtils.DeserializeFromFile<StartingItemsFile>(_dirs.StartingItemsDestinationPath)!.Set;
    }
}
