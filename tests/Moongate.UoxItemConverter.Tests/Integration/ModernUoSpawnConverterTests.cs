using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.Ultima.Types;
using Moongate.UoxItemConverter.Internal;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class ModernUoSpawnConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "moongate-modernuo-" + Guid.NewGuid().ToString("N"));
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string Source => Path.Combine(_root, "Spawns");

    private string Mobiles => Path.Combine(_root, "mobiles");

    private string Destination => Path.Combine(_root, "spawns");

    private string CombinedOutput => _output + _error.ToString();

    public ModernUoSpawnConverterTests()
    {
        AppContext.SetSwitch("Tomlyn.TomlSerializer.IsReflectionEnabledByDefault", true);
        Directory.CreateDirectory(Mobiles);
        File.WriteAllText(
            Path.Combine(Mobiles, "monsters.toml"),
            """
            [[mobile]]
            id = "great_hart"
            [[mobile]]
            id = "dullcopperele"
            [[mobile]]
            id = "banker"
            [[mobile]]
            id = "orcmage"
            [[mobile]]
            id = "earthele"
            """
        );
    }

    [Fact]
    public void Run_TheSpawnersOfAMap_BecomeSpawnRegions_WithTheirMobilesResolved()
    {
        WriteSpawners(
            "shared/malas/Outdoors.json",
            Spawner(1000, 500, 10, 5, 3, "00:05:00", "00:10:00", "GreatHart", "DullCopperElemental"),
            Spawner(20, 30, -5, 0, 2, "00:00:30", "00:01:00", "Minter", "OrcishMage", "EarthElemental")
        );

        Assert.True(Run(MapType.Malas) == 0, CombinedOutput);

        var spawns = Read("malas", "modernuo_outdoors");
        Assert.Equal(
            ["malas_modernuo_shared_outdoors_0", "malas_modernuo_shared_outdoors_1"],
            spawns.Select(spawn => spawn.Id)
        );

        var hart = spawns[0];
        Assert.Equal(MapType.Malas, hart.Map);
        Assert.Equal(["great_hart", "dullcopperele"], hart.MobileIds);
        Assert.Equal((3, 5, 10, 1), (hart.Max, hart.MinMinutes, hart.MaxMinutes, hart.Call));
        var area = Assert.Single(hart.Areas);
        Assert.Equal((995, 495, 1005, 505), (area.X1, area.Y1, area.X2, area.Y2));
        Assert.Equal(26, hart.Z);

        // A spawner without a home range spawns on its spot, as in ModernUO; a short delay is a minute at least.
        var bank = spawns[1];
        Assert.Equal(["banker", "orcmage", "earthele"], bank.MobileIds);
        Assert.Equal((1, 1), (bank.MinMinutes, bank.MaxMinutes));
        Assert.Equal((20, 30, 20, 30), (bank.Areas[0].X1, bank.Areas[0].Y1, bank.Areas[0].X2, bank.Areas[0].Y2));
    }

    [Fact]
    public void Run_ASpawnerOnAnotherMapThanItsFolder_KeepsItsOwnMap()
    {
        // As the Yomotsu Mines and the Fan Dancer's Dojo: in ModernUO's tokuno folder, on the Malas map.
        WriteSpawners(
            "shared/tokuno/YomutsoMines.json",
            Spawner(100, 80, 0, 5, 2, "00:05:00", "00:10:00", "EarthElemental"),
            Spawner(700, 1200, 25, 5, 2, "00:05:00", "00:10:00", "GreatHart")
                .Replace("\"map\": \"Malas\"", "\"map\": \"Tokuno\""),
            Spawner(701, 1201, 25, 5, 2, "00:05:00", "00:10:00", "GreatHart").Replace(", \"map\": \"Malas\",", ",")
        );

        Assert.True(Run(MapType.Tokuno) == 0, CombinedOutput);

        // The ids stay with the source folder; each region goes into the folder of its map, where the server reads the
        // map from. A spawner that names no map takes the folder's.
        Assert.Equal(
            [("tokuno_modernuo_shared_yomutso_mines_0", MapType.Malas, "Malas earthele")],
            Read("malas", "modernuo_yomutso_mines").Select(spawn => (spawn.Id, spawn.Map, spawn.Name))
        );
        Assert.Equal(
            [
                ("tokuno_modernuo_shared_yomutso_mines_1", MapType.Tokuno, "Tokuno great_hart"),
                ("tokuno_modernuo_shared_yomutso_mines_2", MapType.Tokuno, "Tokuno great_hart")
            ],
            Read("tokuno", "modernuo_yomutso_mines").Select(spawn => (spawn.Id, spawn.Map, spawn.Name))
        );
    }

    [Theory, InlineData(MapType.Tokuno, MapType.Malas), InlineData(MapType.Malas, MapType.Tokuno)]
    public void Run_ARegionMovedToAnotherConvertedMap_SurvivesItsOldFiles_WhateverTheOrder(MapType first, MapType second)
    {
        WriteSpawners(
            "shared/tokuno/YomutsoMines.json",
            Spawner(100, 80, 0, 5, 2, "00:05:00", "00:10:00", "EarthElemental")
        );
        WriteSpawners("shared/malas/Vendors.json", Spawner(10, 10, 0, 2, 1, "00:05:00", "00:10:00", "Minter"));
        Directory.CreateDirectory(Path.Combine(Destination, "malas"));
        File.WriteAllText(Path.Combine(Destination, "malas", "modernuo_gone.toml"), "");

        Assert.True(Run(first, second) == 0, CombinedOutput);

        Assert.Single(Read("malas", "modernuo_yomutso_mines"));
        Assert.Single(Read("malas", "modernuo_vendors"));
        Assert.False(File.Exists(Path.Combine(Destination, "malas", "modernuo_gone.toml")));
        Assert.False(File.Exists(Path.Combine(Destination, "tokuno", "modernuo_yomutso_mines.toml")));
    }

    [Fact]
    public void Run_AnEntryCappedBelowTheCount_GetsItsOwnRegion_AndUnknownEntriesKeepTheirShare()
    {
        WriteSpawners(
            "post-uoml/malas/South.json",
            Spawner(500, 500, 0, 10, 10, "00:05:00", "00:10:00", "GreatHart", "Slith")
                .Replace(
                    "\"entries\": [",
                    "\"entries\": [{ \"name\": \"Minter\", \"maxCount\": 1, \"probability\": 100 },"
                )
        );

        Assert.True(Run(MapType.Malas) == 0, CombinedOutput);

        // ModernUO: at most one banker; the other 9 picks split between the hart and the unknown slith.
        var spawns = Read("malas", "modernuo_south");
        Assert.Equal(
            [
                ("malas_modernuo_post_uoml_south_0", "great_hart", 5),
                ("malas_modernuo_post_uoml_south_0_banker", "banker", 1)
            ],
            spawns.Select(spawn => (spawn.Id, Assert.Single(spawn.MobileIds), spawn.Max))
        );
    }

    [Fact]
    public void Run_ACappedClassListedTwice_IsOneRegionWithBothCaps()
    {
        var twice = "{ \"name\": \"Minter\", \"maxCount\": 1, \"probability\": 100 },";
        WriteSpawners(
            "shared/malas/Bedlam.json",
            Spawner(500, 500, 0, 10, 10, "00:05:00", "00:10:00", "GreatHart")
                .Replace("\"entries\": [", "\"entries\": [" + twice + twice)
        );

        Assert.True(Run(MapType.Malas) == 0, CombinedOutput);

        Assert.Equal(
            [("malas_modernuo_shared_bedlam_0", 8), ("malas_modernuo_shared_bedlam_0_banker", 2)],
            Read("malas", "modernuo_bedlam").Select(spawn => (spawn.Id, spawn.Max))
        );
    }

    [Fact]
    public void Run_SpawnBounds_AreTheArea_AndTheirTopTheCeiling()
    {
        WriteSpawners(
            "shared/tokuno/TownsLife.json",
            Spawner(713, 1351, 25, 0, 4, "00:05:00", "00:10:00", "GreatHart")
                .Replace("\"map\": \"Malas\"", "\"map\": \"Tokuno\"")
                .Replace(
                    "\"team\": 0,",
                    "\"team\": 0, \"spawnBounds\": { \"start\": { \"x\": 693, \"y\": 1331, \"z\": -128 }, \"end\": { \"x\": 733, \"y\": 1371, \"z\": 40 } },"
                )
        );

        Assert.True(Run(MapType.Tokuno) == 0, CombinedOutput);

        var spawn = Assert.Single(Read("tokuno", "modernuo_towns_life"));
        Assert.Equal(
            (693, 1331, 733, 1371, 40),
            (spawn.Areas[0].X1, spawn.Areas[0].Y1, spawn.Areas[0].X2, spawn.Areas[0].Y2, spawn.Z)
        );
    }

    [Fact]
    public void Run_AMobilesFolderWithoutTemplates_IsAnError_AndKeepsTheOldFiles()
    {
        WriteSpawners("shared/malas/Vendors.json", Spawner(10, 10, 0, 2, 1, "00:05:00", "00:10:00", "Minter"));
        Directory.CreateDirectory(Path.Combine(Destination, "malas"));
        File.WriteAllText(Path.Combine(Destination, "malas", "modernuo_vendors.toml"), "");

        Assert.Equal(
            2,
            ModernUoSpawnConverter.Run(Source, [MapType.Malas], Path.Combine(_root, "nowhere"), Destination, _output, _error)
        );

        Assert.Contains("no mobile templates", _error.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Destination, "malas", "modernuo_vendors.toml")));
    }

    [Fact]
    public void Run_UnknownMobiles_AreReported_AndASpawnerWithNoneKnownIsSkipped()
    {
        WriteSpawners(
            "post-uoml/termur/Outdoors.json",
            Spawner(100, 100, 0, 5, 2, "00:05:00", "00:10:00", "Slith", "GreatHart")
                .Replace("\"map\": \"Malas\"", "\"map\": \"TerMur\""),
            Spawner(200, 200, 0, 5, 2, "00:05:00", "00:10:00", "Slith").Replace("\"map\": \"Malas\"", "\"map\": \"TerMur\"")
        );

        Assert.True(Run(MapType.TerMur) == 0, CombinedOutput);

        Assert.Equal(["great_hart"], Assert.Single(Read("termur", "modernuo_outdoors")).MobileIds);
        Assert.Contains("unknown mobile Slith", _output.ToString(), StringComparison.Ordinal);
        Assert.Contains("spawner without known mobiles skipped", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ReadsSharedAndPostUoml_OfTheChosenMapsOnly_AndReplacesItsOwnOldFiles()
    {
        WriteSpawners("shared/malas/Vendors.json", Spawner(10, 10, 0, 2, 1, "00:05:00", "00:10:00", "Minter"));
        WriteSpawners("post-uoml/malas/Vendors.json", Spawner(40, 40, 0, 2, 1, "00:05:00", "00:10:00", "Minter"));
        WriteSpawners("uoml/malas/Vendors.json", Spawner(70, 70, 0, 2, 1, "00:05:00", "00:10:00", "Minter"));
        WriteSpawners("shared/tokuno/Vendors.json", Spawner(10, 10, 0, 2, 1, "00:05:00", "00:10:00", "Minter"));
        Directory.CreateDirectory(Path.Combine(Destination, "malas"));
        File.WriteAllText(Path.Combine(Destination, "malas", "modernuo_gone.toml"), "");
        File.WriteAllText(Path.Combine(Destination, "malas", "town_luna.toml"), "");

        Assert.True(Run(MapType.Malas) == 0, CombinedOutput);

        Assert.Equal([10, 40], Read("malas", "modernuo_vendors").Select(spawn => spawn.Areas[0].X1 + 2));
        Assert.False(File.Exists(Path.Combine(Destination, "malas", "modernuo_gone.toml")));
        Assert.True(File.Exists(Path.Combine(Destination, "malas", "town_luna.toml")));
        Assert.False(Directory.Exists(Path.Combine(Destination, "tokuno")));
    }

    [Fact]
    public void Run_AMissingSource_IsAnError()
    {
        Assert.Equal(2, Run(MapType.Malas));
        Assert.Contains("does not exist", _error.ToString(), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private int Run(params MapType[] maps)
    {
        return ModernUoSpawnConverter.Run(Source, maps, Mobiles, Destination, _output, _error);
    }

    private List<SpawnTemplate> Read(string map, string file)
    {
        return TomlUtils.DeserializeFromFile<SpawnTemplateFile>(Path.Combine(Destination, map, file + ".toml"))!.Spawn;
    }

    private void WriteSpawners(string relative, params string[] spawners)
    {
        var path = Path.Combine(Source, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "[" + string.Join(",", spawners) + "]");
    }

    private static string Spawner(
        int x, int y, int z, int homeRange, int count, string minDelay, string maxDelay, params string[] names
    )
    {
        var entries = string.Join(
            ",",
            names.Select(name => $$"""{ "name": "{{name}}", "maxCount": {{count}}, "probability": 100 }""")
        );

        return $$"""
                 {
                   "$type": "Spawner", "name": "Spawner (1)", "location": [{{x}}, {{y}}, {{z}}], "map": "Malas",
                   "count": {{count}}, "minDelay": "{{minDelay}}", "maxDelay": "{{maxDelay}}", "team": 0,
                   "homeRange": {{homeRange}}, "walkingRange": 2, "entries": [{{entries}}]
                 }
                 """;
    }
}
