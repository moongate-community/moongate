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
        Assert.Equal(["malas_modernuo_outdoors_0", "malas_modernuo_outdoors_1"], spawns.Select(spawn => spawn.Id));

        var hart = spawns[0];
        Assert.Equal(MapType.Malas, hart.Map);
        Assert.Equal(["great_hart", "dullcopperele"], hart.MobileIds);
        Assert.Equal((3, 5, 10, 1), (hart.Max, hart.MinMinutes, hart.MaxMinutes, hart.Call));
        var area = Assert.Single(hart.Areas);
        Assert.Equal((995, 495, 1005, 505), (area.X1, area.Y1, area.X2, area.Y2));
        Assert.Equal(26, hart.Z);

        // A spawner without a home range still spawns around its spot; a short delay is a minute at least.
        var bank = spawns[1];
        Assert.Equal(["banker", "orcmage", "earthele"], bank.MobileIds);
        Assert.Equal((1, 1), (bank.MinMinutes, bank.MaxMinutes));
        Assert.Equal((19, 29, 21, 31), (bank.Areas[0].X1, bank.Areas[0].Y1, bank.Areas[0].X2, bank.Areas[0].Y2));
    }

    [Fact]
    public void Run_UnknownMobiles_AreReported_AndASpawnerWithNoneKnownIsSkipped()
    {
        WriteSpawners(
            "post-uoml/termur/Outdoors.json",
            Spawner(100, 100, 0, 5, 2, "00:05:00", "00:10:00", "Slith", "GreatHart"),
            Spawner(200, 200, 0, 5, 2, "00:05:00", "00:10:00", "Slith")
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

    private static string Spawner(int x, int y, int z, int homeRange, int count, string minDelay, string maxDelay, params string[] names)
    {
        var entries = string.Join(",", names.Select(name => $$"""{ "name": "{{name}}", "maxCount": {{count}}, "probability": 100 }"""));

        return $$"""
                 {
                   "$type": "Spawner", "name": "Spawner (1)", "location": [{{x}}, {{y}}, {{z}}], "map": "Malas",
                   "count": {{count}}, "minDelay": "{{minDelay}}", "maxDelay": "{{maxDelay}}", "team": 0,
                   "homeRange": {{homeRange}}, "walkingRange": 2, "entries": [{{entries}}]
                 }
                 """;
    }
}
