using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.Ultima.Types;
using Moongate.UoxItemConverter.Internal;
using Moongate.UoxItemConverter.Tests.TestSupport;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class UoxSpawnConverterTests : IDisposable
{
    private readonly ConverterTestDirectories _dirs = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string CombinedOutput => _output + _error.ToString();

    public UoxSpawnConverterTests()
    {
        AppContext.SetSwitch("Tomlyn.TomlSerializer.IsReflectionEnabledByDefault", true);
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
    }

    [Fact]
    public void Run_NpcLists_BecomeWeightedListsOfMobilesAndNestedLists_DroppingUnknownOnes()
    {
        WriteSources(
            npcLists: """
                      [NPCLIST jungle]
                      {
                      20|Gorilla
                      orc
                      7|NPCLIST=trolls
                      unicorn
                      }
                      [NPCLIST trolls]
                      {
                      troll
                      }
                      """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var lists = TomlUtils.DeserializeFromFile<NpcListTemplateFile>(Path.Combine(_dirs.NpcListsDestinationDirectory, "npclists.toml"))!
                             .NpcList.ToDictionary(list => list.Id);
        Assert.Equal(
            ["gorilla:20", "orc:1", "list trolls:7"],
            lists["jungle"].Entries.Select(entry => entry.MobileId is { } id ? $"{id}:{entry.Weight}" : $"list {entry.NpcListId}:{entry.Weight}")
        );
        Assert.Equal(["troll"], lists["trolls"].Entries.Select(entry => entry.MobileId));
    }

    [Fact]
    public void Run_SpawnRegions_BecomeSpawnsOnTheirMap_WithGetInheritance_AndItemOnlyRegionsSkipped()
    {
        WriteSources(
            spawns: """
                    [REGIONSPAWN 0]
                    {
                    NAME=The Hammer And Anvil
                    NPC=orc
                    MAXNPCS=2
                    X1=1422
                    Y1=1547
                    X2=1426
                    Y2=1550
                    WORLD=1
                    MINTIME=480
                    MAXTIME=600
                    CALL=1
                    ONLYOUTSIDE=1
                    EXCLUDEAREA=1423,1548,1424,1549
                    PREFZ=22
                    }
                    [REGIONSPAWN 1]
                    {
                    GET=0
                    NAME=Next Door
                    NPCLIST=jungle
                    NPCLIST=trolls
                    DEFZ=36
                    }
                    [REGIONSPAWN 2]
                    {
                    NAME=Treasure
                    ITEMLIST=dungeon_treasure
                    MAXITEMS=6
                    X1=1
                    Y1=1
                    X2=5
                    Y2=5
                    WORLD=1
                    }
                    """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var spawns = TomlUtils.DeserializeFromFile<SpawnTemplateFile>(Path.Combine(_dirs.SpawnsDestinationDirectory, "trammel", "town_test.toml"))!
                              .Spawn;
        Assert.Equal(["trammel_0", "trammel_1"], spawns.Select(spawn => spawn.Id));

        var shop = spawns[0];
        Assert.Equal(
            (MapType.Trammel, "The Hammer And Anvil", 2, 480, 600, 1, true, (int?)22, (int?)null),
            (shop.Map, shop.Name, shop.Max, shop.MinMinutes, shop.MaxMinutes, shop.Call, shop.OnlyOutside, shop.PrefZ, shop.Z)
        );
        Assert.Equal(["orc"], shop.MobileIds);
        Assert.Equal((1422, 1547, 1426, 1550), (shop.Areas[0].X1, shop.Areas[0].Y1, shop.Areas[0].X2, shop.Areas[0].Y2));
        Assert.Equal((1423, 1548, 1424, 1549), (shop.Exclude[0].X1, shop.Exclude[0].Y1, shop.Exclude[0].X2, shop.Exclude[0].Y2));

        var next = spawns[1];
        Assert.Equal(("Next Door", 2, 480, (int?)36), (next.Name, next.Max, next.MinMinutes, next.Z));
        Assert.Empty(next.MobileIds);
        Assert.Equal(["jungle", "trolls"], next.NpcListIds);
        Assert.Single(next.Areas);
        Assert.Contains("1 x spawn region(s) without NPCs skipped", CombinedOutput);
    }

    [Fact]
    public void Run_TwoFilesWithRegionsOfTheSameMapAndName_AreWrittenTogether()
    {
        WriteSources(spawns: Region(0, world: 0));
        _dirs.WriteMobileSource("spawn/trammel/spawn_trammel_town_test.dfn", Region(1, world: 0) + Region(2, world: 1));

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal(
            ["felucca_0", "felucca_1"],
            TomlUtils.DeserializeFromFile<SpawnTemplateFile>(Path.Combine(_dirs.SpawnsDestinationDirectory, "felucca", "town_test.toml"))!
                     .Spawn.Select(spawn => spawn.Id)
        );
        Assert.Equal(
            ["trammel_2"],
            TomlUtils.DeserializeFromFile<SpawnTemplateFile>(Path.Combine(_dirs.SpawnsDestinationDirectory, "trammel", "town_test.toml"))!
                     .Spawn.Select(spawn => spawn.Id)
        );
    }

    private static string Region(int number, int world)
    {
        return $"[REGIONSPAWN {number}]\n{{\nNPC=orc\nMAXNPCS=1\nX1=1\nY1=1\nX2=5\nY2=5\nWORLD={world}\n}}\n";
    }

    [Fact]
    public void Run_ASpawnOfAnUnknownMobile_IsDropped()
    {
        WriteSources(
            spawns: """
                    [REGIONSPAWN 0]
                    {
                    NAME=Nobody
                    NPC=dragon_king
                    MAXNPCS=1
                    X1=1
                    Y1=1
                    X2=5
                    Y2=5
                    WORLD=0
                    }
                    """
        );

        Assert.True(Run() == 0, CombinedOutput);

        Assert.False(File.Exists(Path.Combine(_dirs.SpawnsDestinationDirectory, "felucca", "town_test.toml")));
        Assert.Contains("unresolved spawn", CombinedOutput);
    }

    [Fact]
    public void Run_TheSpawnOptions_NeedTheMobileSource()
    {
        _dirs.WriteSource("items.dfn", "[coin]\n{\nid=0x0eed\n}\n");

        var exitCode = UoxItemConverterCommand.Run(
            _dirs.SourceDirectory,
            _dirs.DestinationDirectory,
            null,
            _output,
            _error,
            npcListsDestination: _dirs.NpcListsDestinationDirectory,
            spawnsDestination: _dirs.SpawnsDestinationDirectory
        );

        Assert.Equal(2, exitCode);
        Assert.Contains("--npc-lists-destination and --spawns-destination need --mobile-source", _error.ToString());
    }

    public void Dispose()
    {
        _dirs.Dispose();
    }

    private void WriteSources(string? npcLists = null, string? spawns = null)
    {
        _dirs.WriteSource("items.dfn", "[coin]\n{\nid=0x0eed\n}\n");
        _dirs.WriteMobileSource("npc/namelists.dfn", "");
        _dirs.WriteMobileSource("../dictionaries/dictionary.ENG", "");
        _dirs.WriteMobileSource(
            "npc/monsters.dfn",
            "[orc]\n{\nNAME=an orc\nID=0x0011\n}\n[troll]\n{\nNAME=a troll\nID=0x0036\n}\n[gorilla]\n{\nNAME=a gorilla\nID=0x001D\n}\n"
        );
        _dirs.WriteMobileSource("npc/npclists/npclists.dfn", npcLists ?? "[NPCLIST jungle]\n{\norc\n}\n[NPCLIST trolls]\n{\ntroll\n}\n");
        _dirs.WriteMobileSource("spawn/felucca/spawn_felucca_town_test.dfn", spawns ?? "");
    }

    private int Run()
    {
        return UoxItemConverterCommand.Run(
            _dirs.SourceDirectory,
            _dirs.DestinationDirectory,
            null,
            _output,
            _error,
            _dirs.MobileSourceDirectory,
            _dirs.MobileDestinationDirectory,
            _dirs.NamesDestinationPath,
            npcListsDestination: _dirs.NpcListsDestinationDirectory,
            spawnsDestination: _dirs.SpawnsDestinationDirectory
        );
    }
}
