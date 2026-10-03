using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.UoxItemConverter.Internal;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class ModernUoChestConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "moongate-modernuo-chests-" + Guid.NewGuid().ToString("N"));
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string Source => Path.Combine(_root, "Spawns");

    private string Destination => Path.Combine(_root, "spawns");

    private string CombinedOutput => _output + _error.ToString();

    [Fact]
    public void Run_AChestSpawner_BecomesARegionOfItems_AroundItsSpot()
    {
        Write(
            "shared/felucca/Shame.json",
            """
            [ { "location": [5400, 20, 10], "count": 1, "minDelay": "00:05:00", "maxDelay": "00:10:00", "homeRange": 2,
                "entries": [ { "name": "TreasureChestLevel3", "maxCount": 1, "probability": 100 } ] } ]
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var spawn = Assert.Single(Read("felucca"));
        Assert.Equal("felucca_chest_shared_shame_0_level_3", spawn.Id);
        Assert.Equal(["treasure_chest_level_3"], spawn.ItemIds);
        Assert.Empty(spawn.MobileIds);
        Assert.Equal((1, 1, 5, 10), (spawn.Max, spawn.Call, spawn.MinMinutes, spawn.MaxMinutes));
        var area = Assert.Single(spawn.Areas);
        Assert.Equal((5398, 18, 5402, 22), (area.X1, area.Y1, area.X2, area.Y2));
        // Under the spawner's ceiling, so a chest of a cave is not put on the hill over it.
        Assert.Equal(26, spawn.Z);
        Assert.Equal("Treasure chest level 3", spawn.Name);
        Assert.Contains("1 chest region(s)", _output.ToString());
    }

    [Fact]
    public void Run_ASpawnerOfChestsAndCreatures_GivesItsChestsOnly_EachLevelWithItsCap()
    {
        Write(
            "shared/trammel/Deceit.json",
            """
            [ { "location": [100, 200, 0], "count": 3, "minDelay": "00:05:00", "maxDelay": "00:10:00", "homeRange": 0,
                "entries": [ { "name": "Lich", "maxCount": 3 },
                             { "name": "TreasureChestLevel1", "maxCount": 1 },
                             { "name": "TreasureChestLevel2", "maxCount": 5 } ] },
              { "location": [1, 2, 0], "count": 1, "minDelay": "00:05:00", "maxDelay": "00:10:00",
                "entries": [ { "name": "Orc", "maxCount": 1 } ] } ]
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var spawns = Read("trammel");
        Assert.Equal(["trammel_chest_shared_deceit_0_level_1", "trammel_chest_shared_deceit_0_level_2"], spawns.Select(spawn => spawn.Id));
        // A cap above the spawner's count is the count.
        Assert.Equal([1, 3], spawns.Select(spawn => spawn.Max));
        Assert.Equal((100, 200, 100, 200), (spawns[0].Areas[0].X1, spawns[0].Areas[0].Y1, spawns[0].Areas[0].X2, spawns[0].Areas[0].Y2));
    }

    [Fact]
    public void Run_EveryMapAndEra_GoIntoOneFilePerMap_AndAPreviousFileIsReplaced()
    {
        const string chest = """[ { "location": [1, 2, 0], "count": 1, "minDelay": "00:05:00", "maxDelay": "00:10:00", "entries": [ { "name": "TreasureChestLevel4" } ] } ]""";
        Write("shared/felucca/A.json", chest);
        Write("post-uoml/felucca/B.json", chest);
        Write("shared/ilshenar/C.json", chest);
        // An era of an old client is not read.
        Write("uoml/felucca/D.json", chest);
        Directory.CreateDirectory(Path.Combine(Destination, "felucca"));
        File.WriteAllText(Path.Combine(Destination, "felucca", "treasure_chests.toml"), "[[spawn]]\nid = \"old\"\n");
        File.WriteAllText(Path.Combine(Destination, "felucca", "dungeon_shame.toml"), "# kept\n");

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal(
            ["felucca_chest_post_uoml_b_0_level_4", "felucca_chest_shared_a_0_level_4"],
            Read("felucca").Select(spawn => spawn.Id).Order()
        );
        Assert.Single(Read("ilshenar"));
        Assert.Equal("# kept\n", File.ReadAllText(Path.Combine(Destination, "felucca", "dungeon_shame.toml")));
    }

    [Fact]
    public void Run_ALevelListedTwiceInASpawner_IsOneRegionWithBothCaps()
    {
        Write(
            "shared/felucca/A.json",
            """
            [ { "location": [1, 2, 0], "count": 5, "minDelay": "00:05:00", "maxDelay": "00:10:00",
                "entries": [ { "name": "TreasureChestLevel2", "maxCount": 1 }, { "name": "TreasureChestLevel2", "maxCount": 2 },
                             { "name": "TreasureChestLevel3", "maxCount": 4 }, { "name": "TreasureChestLevel3", "maxCount": 4 } ] } ]
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var spawns = Read("felucca");
        Assert.Equal(["felucca_chest_shared_a_0_level_2", "felucca_chest_shared_a_0_level_3"], spawns.Select(spawn => spawn.Id));
        // Together at most the spawner's count.
        Assert.Equal([3, 5], spawns.Select(spawn => spawn.Max));
    }

    [Fact]
    public void Run_AChestOfALevelWithoutTemplate_IsCountedAndLeftOut()
    {
        Write(
            "shared/felucca/A.json",
            """
            [ { "location": [1, 2, 0], "count": 1, "minDelay": "00:05:00", "maxDelay": "00:10:00",
                "entries": [ { "name": "TreasureChestLevel9" }, { "name": "treasurechestlevel2" } ] } ]
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal(["treasure_chest_level_2"], Assert.Single(Read("felucca")).ItemIds);
        Assert.Contains("TreasureChestLevel9", _output.ToString());
    }

    [Fact]
    public void Run_NoChestAnywhere_Fails_AndWritesNothing()
    {
        Write("shared/felucca/A.json", """[ { "location": [1, 2, 0], "count": 1, "entries": [ { "name": "Orc" } ] } ]""");

        Assert.Equal(2, Run());

        Assert.Contains("no treasure chest", _error.ToString());
        Assert.False(Directory.Exists(Destination));
    }

    [Fact]
    public void Run_AMissingFolder_Fails()
    {
        Assert.Equal(2, Run());

        Assert.Contains("does not exist", _error.ToString());
    }

    private void Write(string path, string json)
    {
        var file = Path.Combine(Source, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, json);
    }

    private int Run()
    {
        return ModernUoChestConverter.Run(Source, Destination, _output, _error);
    }

    private List<SpawnTemplate> Read(string map)
    {
        return TomlUtils.DeserializeFromFile<SpawnTemplateFile>(Path.Combine(Destination, map, "treasure_chests.toml"))!.Spawn;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
