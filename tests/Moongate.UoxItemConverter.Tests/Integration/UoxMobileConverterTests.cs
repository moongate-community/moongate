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
    public void Run_TheNamesHeader_IsFollowedByABlankLine()
    {
        WriteItemsAndNames();

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Contains("====\n\n[[names]]", File.ReadAllText(_dirs.NamesDestinationPath));
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
     InlineData("ID=0x0190", RaceType.Human, MobileGenderType.Male, null, "male"),
     InlineData("ID=0x0191", RaceType.Human, MobileGenderType.Female, null, "female"),
     InlineData("ID=0x025E", RaceType.Elf, MobileGenderType.Female, null, "female"),
     InlineData("ID=0x0033\nRACE=22", null, null, 0x33, "female")]
    public void Run_HumanoidBodiesBecomeRaceAndGender(
        string lines,
        RaceType? race,
        MobileGenderType? gender,
        int? body,
        string nameList
    )
    {
        WriteItemsAndNames();
        _dirs.WriteMobileSource("npc/a.dfn", $"[x]\n{{\n{lines}\nNAMELIST=2\n}}\n");

        Assert.True(Run() == 0, CombinedOutput);

        var x = ReadMobiles("a.toml")["x"];
        // NAMELIST=2 (female) follows the gender on a male body.
        Assert.Equal((race, gender, body, nameList), (x.Race, x.Gender, x.Body, x.NameList));
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

    [Fact]
    public void Run_ADoubledHexPrefix_IsReadAsOne()
    {
        // UOX3's base_raiju has ID=0x0xc7.
        WriteItemsAndNames();
        _dirs.WriteMobileSource("npc/a.dfn", "[x]\n{\nID=0x0xc7\n}\n");

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal(0xC7, ReadMobiles("a.toml")["x"].Body);
    }

    [Fact]
    public void Run_TheMobilesMoongateHasAScriptFor_GetTheirScriptId()
    {
        WriteItemsAndNames();
        _dirs.WriteMobileSource(
            "npc/undead.dfn",
            "[skeleton]\n{\nID=0x0032\n}\n[zombie]\n{\nID=0x0003\n}\n[wraith]\n{\nID=0x001a\n}\n[ghoul]\n{\nGET=wraith\n}\n" +
            "[spectre]\n{\nID=0x001a\n}\n[lich]\n{\nID=0x0018\n}\n[headless]\n{\nID=0x001f\n}\n[boneknight]\n{\nGET=skeleton\n}\n[m_banker]\n{\nID=0x0190\nNPCAI=8\n}\n" +
            "[orc]\n{\nID=0x0011\nNPCAI=2\n}\n"
        );

        Assert.True(Run() == 0, CombinedOutput);

        var mobiles = ReadMobiles("undead.toml");
        Assert.Equal(
            ("monster", "monster", "banker"),
            (mobiles["skeleton"].ScriptId, mobiles["zombie"].ScriptId, mobiles["m_banker"].ScriptId)
        );
        Assert.All(
            new[] { "wraith", "ghoul", "spectre", "lich", "headless" },
            id => Assert.Equal("monster", mobiles[id].ScriptId)
        );
        // A template based on one of them takes the script through its base; the others have none yet.
        Assert.True(string.IsNullOrEmpty(mobiles["boneknight"].ScriptId));
        Assert.True(string.IsNullOrEmpty(mobiles["orc"].ScriptId));
    }

    [Fact]
    public void Run_ARandomPickOfTwoCreatures_BecomesTheFirst()
    {
        // UOX3's [dragon] GET=graydragon reddragon: a template has one base, so the first is kept and counted.
        WriteItemsAndNames();
        _dirs.WriteMobileSource(
            "npc/dragons.dfn",
            "[graydragon]\n{\nID=0x000c\n}\n[reddragon]\n{\nID=0x003b\n}\n[dragon]\n{\nGET=graydragon reddragon\n}\n"
        );

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal("graydragon", ReadMobiles("dragons.toml")["dragon"].BaseId);
        Assert.Contains("1 x two-target get, first target kept", _output.ToString());
    }

    [Fact]
    public void Run_AnFPrefixedNpcWithAMaleBody_IsFemale_SoItsPairMerges()
    {
        // UOX3's femalevendors.dfn gives [f_scribe] the male body 0x0190.
        WriteItemsAndNames();
        _dirs.WriteMobileSource(
            "npc/vendors.dfn",
            "[m_scribe]\n{\nID=0x0190\nNAMELIST=1\n}\n[f_scribe]\n{\nID=0x0190\nNAMELIST=2\n}\n[scribe]\n{\nGET=m_scribe f_scribe\n}\n"
        );

        Assert.True(Run() == 0, CombinedOutput);

        var mobiles = ReadMobiles("vendors.toml");
        Assert.Equal(MobileGenderType.Female, mobiles["f_scribe"].Gender);
        Assert.Equal((MobileGenderType.Random, "{gender}"), (mobiles["scribe"].Gender!.Value, mobiles["scribe"].NameList));
    }

    [Fact]
    public void Run_ARaceOnANonHumanBody_IsDropped()
    {
        // UOX3's [giantrat] has RACE=2 (gargoyle) on the rat body.
        WriteItemsAndNames();
        _dirs.WriteMobileSource("npc/a.dfn", "[giantrat]\n{\nID=0x00d7\nRACE=2\n}\n");

        Assert.True(Run() == 0, CombinedOutput);

        var rat = ReadMobiles("a.toml")["giantrat"];
        Assert.Equal(((int?)0xD7, (RaceType?)null), (rat.Body, rat.Race));
    }

    [Theory, InlineData("ID=0x0191\nNAMELIST=1", "female"), InlineData("ID=0x0190\nNAMELIST=2", "male")]
    public void Run_AMaleOrFemaleNameList_FollowsTheGender(string lines, string list)
    {
        // UOX3's [f_paladin] and others use the male list on a female body.
        WriteItemsAndNames();
        _dirs.WriteMobileSource("npc/a.dfn", $"[x]\n{{\n{lines}\n}}\n");

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal(list, ReadMobiles("a.toml")["x"].NameList);
    }

    [Fact]
    public void Run_NumbersBecomeDice()
    {
        WriteItemsAndNames();
        _dirs.WriteMobileSource(
            "npc/a.dfn",
            """
            [x]
            {
            ID=0x0011
            STR=96 120
            DEX=50
            HPMAX=58 72
            HP=1
            DAMAGE=3 9
            DEF=14
            RESISTFIRE=20 30
            ELEMENTRESIST=10 11 12 13
            MAGERY=500 700
            MAGICRESISTANCE=655
            SWORDSMANSHIP=1000 1500
            SWORDFIGHTING=500
            KARMA=-2500
            FAME=2500
            GOLD=0 50
            FLAG=NEUTRAL
            CUSTOMINTTAG=Level 7
            }
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var x = ReadMobiles("a.toml")["x"];
        Assert.Equal("1d25+95", x.Strength!.Value.ToString());
        Assert.Equal(50, x.Dexterity!.Value.Roll());
        Assert.Equal("1d15+57", x.Hits!.Value.ToString());
        Assert.Equal("1d7+2", x.Damage!.Value.ToString());
        Assert.Equal(14, x.Armor!.Value.Roll());
        Assert.Equal(
            (10, 11, 13, 12),
            (x.Resistances!.Fire!.Value.Roll(), x.Resistances.Cold!.Value.Roll(), x.Resistances.Poison!.Value.Roll(),
             x.Resistances.Energy!.Value.Roll())
        );
        Assert.Equal("1d21+49", x.Skills!["magery"].ToString());
        Assert.Equal(65, x.Skills["resisting_spells"].Roll());
        Assert.Equal(120, x.Skills["swordsmanship"].Max);
        Assert.Equal(3, x.Skills.Count);
        Assert.Equal((-2500, 2500), (x.Karma!.Value.Roll(), x.Fame!.Value.Roll()));
        Assert.Equal("1d51-1", x.Gold!.Value.ToString());
        Assert.Equal(NotorietyType.Attackable, x.Notoriety);
        Assert.Equal("7", x.Tags!["Level"]);
        x.Validate();
    }

    [Fact]
    public void Run_EquipmentColoursAndLootResolveAgainstTheItems()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [0x13e4]
            {
            id=0x13e4
            }
            [0x1517]
            {
            id=0x1517
            }
            [ITEMLIST 20]
            {
            0x1517
            50|blank
            10|0x13e4
            }
            [ITEMLIST 13]
            {
            0x203b
            }
            [LOOTLIST orcLoot]
            {
            0x13e4
            }
            """
        );
        WriteNames();
        _dirs.WriteMobileSource(
            "colors/colors.dfn",
            "[RANDOMCOLOR 11]\n{\n0x0835\n0x0836\n}\n[RANDOMCOLOR 33]\n{\n0x0003\n0x0059\n}\n"
        );
        _dirs.WriteMobileSource(
            "npc/a.dfn",
            """
            [x]
            {
            COLOR=0x0010
            ID=0x0190
            EQUIPITEM=listobject13
            EQUIPITEM=0x13e4
            COLOR=0x0455
            EQUIPITEM=listobject20
            COLORLIST=11
            EQUIPITEM=0x1f13
            EQUIPITEM=0x13e4
            COLORLIST=33
            LOOT=orcLoot,2
            LOOT=nothing
            }
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var x = ReadMobiles("a.toml")["x"];
        var equipment = x.Equipment!;
        Assert.Equal(3, equipment.Count);
        Assert.Equal(["0x13e4"], equipment[0].Items);
        Assert.Equal("0x0455", equipment[0].Hue.ToString());
        Assert.Equal(["0x1517", "0x13e4"], equipment[1].Items);
        Assert.Equal("0x0835-0x0836", equipment[1].Hue.ToString());
        Assert.Null(equipment[2].Hue);
        Assert.Equal(["orc_loot", "orc_loot"], x.Loot);
        Assert.Contains("unresolved item", CombinedOutput);
        Assert.Contains("unresolved loot", CombinedOutput);
        Assert.Contains("colour list not a range", CombinedOutput);
        Assert.Contains("item list weight or blank dropped", CombinedOutput);
    }

    [Fact]
    public void Run_SoundsComeFromCreaturesOnTheBlockThatSetsTheBody()
    {
        WriteItemsAndNames();
        _dirs.WriteMobileSource(
            "creatures/creatures.dfn",
            "[CREATURE 0x11]\n{ Orc\nSOUND_STARTATTACK=0x1b0\nSOUND_IDLE=0x1b1\nSOUND_ATTACK=0x1b2\nSOUND_DEFEND=0x1b3\nSOUND_DIE=0x1b4\n}\n" +
            "[CREATURE 0x190]\n{ Human Male\nSOUND_DIE=0x15c\n}\n"
        );
        _dirs.WriteMobileSource("npc/a.dfn", "[base_orc]\n{\nID=0x0011\n}\n[orc]\n{\nGET=base_orc\n}\n[man]\n{\nID=0x0190\n}\n");

        Assert.True(Run() == 0, CombinedOutput);

        var mobiles = ReadMobiles("a.toml");
        var sounds = mobiles["base_orc"].Sounds!;
        Assert.Equal(
            ((int?)0x1B0, (int?)0x1B1, (int?)0x1B2, (int?)0x1B3, (int?)0x1B4),
            (sounds.StartAttack, sounds.Idle, sounds.Attack, sounds.Hurt, sounds.Death)
        );
        Assert.Null(mobiles["orc"].Sounds);
        Assert.Equal(((int?)null, (int?)0x15C), (mobiles["man"].Sounds!.Idle, mobiles["man"].Sounds!.Death));
    }

    [Fact]
    public void Run_MovementComesFromCreaturesOnTheBlockThatSetsTheBody_AndALandBodyOverridesAWaterBase()
    {
        WriteItemsAndNames();
        _dirs.WriteMobileSource(
            "creatures/creatures.dfn",
            "[CREATURE 0x97]\n{ Dolphin\nMOVEMENT=WATER\n}\n[CREATURE 0xdd]\n{ Walrus\nMOVEMENT=BOTH\n}\n" +
            "[CREATURE 0x11]\n{ Orc\nMOVEMENT=LAND\n}\n"
        );
        _dirs.WriteMobileSource(
            "npc/a.dfn",
            "[dolphin]\n{\nID=0x0097\n}\n[big_dolphin]\n{\nGET=dolphin\n}\n[walrus]\n{\nID=0x00dd\n}\n[orc]\n{\nID=0x0011\n}\n" +
            "[beached]\n{\nGET=dolphin\nID=0x0011\n}\n"
        );

        Assert.True(Run() == 0, CombinedOutput);

        var mobiles = ReadMobiles("a.toml");
        Assert.Equal(
            [MobileMovementType.Water, null, MobileMovementType.Both, null, MobileMovementType.Land],
            new[] { "dolphin", "big_dolphin", "walrus", "orc", "beached" }.Select(id => mobiles[id].Movement)
        );
    }

    [Fact]
    public void Run_AMaleFemalePair_BecomesOneRandomGenderTemplate_AndOtherPairsAreSkipped()
    {
        _dirs.WriteSource("items.dfn", "[0x13e4]\n{\nid=0x13e4\n}\n[0x1517]\n{\nid=0x1517\n}\n[0x1516]\n{\nid=0x1516\n}\n");
        WriteNames();
        _dirs.WriteMobileSource(
            "npc/a.dfn",
            """
            [basehuman]
            {
            FLAG=INNOCENT
            }
            [m_guard]
            {
            GET=basehuman
            NAMELIST=1
            ID=0x0190
            DEF=20
            EQUIPITEM=0x13e4
            EQUIPITEM=0x1517
            }
            [f_guard]
            {
            GET=basehuman
            NAMELIST=2
            ID=0x0191
            DEF=100
            EQUIPITEM=0x13e4
            EQUIPITEM=0x1516
            }
            [guard]
            {
            GET=m_guard f_guard
            }
            [dragon]
            {
            GET=m_guard basehuman
            }
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var mobiles = ReadMobiles("a.toml");
        var guard = mobiles["guard"];
        Assert.Equal(
            ((MobileGenderType?)MobileGenderType.Random, (RaceType?)RaceType.Human, "{gender}", "basehuman"),
            (guard.Gender, guard.Race, guard.NameList, guard.BaseId)
        );
        Assert.Equal(20, guard.Armor!.Value.Roll());
        Assert.Equal(
            ["0x13e4:", "0x1517:Male", "0x1516:Female"],
            guard.Equipment!.Select(entry => $"{string.Join(",", entry.Items)}:{entry.Gender}")
        );
        Assert.True(mobiles.ContainsKey("m_guard"));
        // Not a gender pair: the first target is kept as its base.
        Assert.Equal("m_guard", mobiles["dragon"].BaseId);
        Assert.Contains("differs between the male and female", CombinedOutput);
        Assert.Contains("two-target get, first target kept", CombinedOutput);
    }

    [Fact]
    public void Run_TheWrittenMobilesAreReadBackAndVerified()
    {
        WriteItemsAndNames();
        _dirs.WriteMobileSource("npc/a.dfn", "[x]\n{\nID=0x0011\nSTR=10\n}\n");

        Assert.True(Run() == 0, CombinedOutput);
        Assert.Contains("Verified 1 mobile(s) and 2 name list(s)", CombinedOutput);
    }

    [Fact]
    public void Run_ATemplateThatFailsValidation_FailsTheRun()
    {
        WriteItemsAndNames();
        _dirs.WriteMobileSource("npc/a.dfn", "[x]\n{\nID=0x0011\nDEF=-5\n}\n");

        Assert.Equal(1, Run());
        Assert.Contains("Mobile template 'x': armor", CombinedOutput);
    }

    [Fact]
    public void Run_APairWithDifferentSounds_LeavesTheSoundsUnset()
    {
        WriteItemsAndNames();
        _dirs.WriteMobileSource(
            "creatures/creatures.dfn",
            "[CREATURE 0x190]\n{\nSOUND_DIE=0x15c\n}\n[CREATURE 0x191]\n{\nSOUND_DIE=0x151\n}\n"
        );
        _dirs.WriteMobileSource(
            "npc/a.dfn",
            "[m_guard]\n{\nID=0x0190\n}\n[f_guard]\n{\nID=0x0191\n}\n[guard]\n{\nGET=m_guard f_guard\n}\n"
        );

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Null(ReadMobiles("a.toml")["guard"].Sounds);
    }

    [Fact]
    public void Run_EquipmentThroughAnItemAlias_FollowsItsEraOrRandomGet()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [0x170b]
            {
            id=0x170b
            }
            [0x170c]
            {
            id=0x170c
            }
            [boots]
            {
            get=0x170b 0x170c
            }
            [0x13bb_lbr]
            {
            id=0x13bb
            }
            [0x13bb]
            {
            getlbr=0x13bb_lbr
            }
            """
        );
        WriteNames();
        _dirs.WriteMobileSource("npc/a.dfn", "[x]\n{\nID=0x0011\nEQUIPITEM=boots\nEQUIPITEM=0x13bb\n}\n");

        Assert.True(Run() == 0, CombinedOutput);

        var equipment = ReadMobiles("a.toml")["x"].Equipment!;
        Assert.Equal(["0x170b", "0x170c"], equipment[0].Items);
        // [0x13bb] getlbr=0x13bb_lbr has one parent, so it is a template of its own inheriting that graphic.
        Assert.Equal(["0x13bb"], equipment[1].Items);
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
