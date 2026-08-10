using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Maps;
using Moongate.Ultima.Tiles;
using Moongate.UO.Data.World;
using Xunit.Abstractions;

namespace Moongate.Tests.Data.World;

/// <summary>A probe: the building the client is standing in, and why the scan found no doorways in it.</summary>
public class BuildingAtClientProbe
{
    private const int ClientX = 3508;
    private const int ClientY = 2554;
    private const int Radius = 25;

    private readonly ITestOutputHelper _output;

    public BuildingAtClientProbe(ITestOutputHelper output)
    {
        _output = output;
    }

    [ClientFilesFact]
    public void ReportTheBuildingHere()
    {
        Files.SetDirectory(ClientFiles.Directory);
        TileData.Initialize();

        var map = new Map(1, 1, 6144, 4096);
        var names = new Dictionary<string, (int Count, int SampleId)>(StringComparer.Ordinal);
        var frames = new List<string>();
        var doorStatics = new List<string>();

        for (var x = ClientX - Radius; x <= ClientX + Radius; x++)
        {
            for (var y = ClientY - Radius; y <= ClientY + Radius; y++)
            {
                foreach (var tile in map.Tiles.GetStaticTiles(x, y))
                {
                    var name = TileData.ItemTable[tile.Id].Name ?? "";
                    names[name] = names.TryGetValue(name, out var entry)
                                      ? (entry.Count + 1, entry.SampleId)
                                      : (1, tile.Id);

                    if (DoorFrames.IsWest(tile.Id) || DoorFrames.IsEast(tile.Id) ||
                        DoorFrames.IsNorth(tile.Id) || DoorFrames.IsSouth(tile.Id))
                    {
                        frames.Add($"{x},{y},{tile.Z} 0x{tile.Id:X4} '{name}'");
                    }

                    // A door drawn INTO the map as a static would render client-side with no item at all.
                    if (name.Contains("door", StringComparison.OrdinalIgnoreCase))
                    {
                        doorStatics.Add($"{x},{y},{tile.Z} 0x{tile.Id:X4} '{name}'");
                    }
                }
            }
        }

        _output.WriteLine($"--- statics by name within {Radius} tiles of {ClientX},{ClientY} ---");

        foreach (var (name, (count, sample)) in names.OrderByDescending(e => e.Value.Count).Take(20))
        {
            _output.WriteLine($"{count,4}  0x{sample:X4}  {name}");
        }

        _output.WriteLine($"--- frames from our tables here: {frames.Count} ---");

        foreach (var frame in frames.Take(10))
        {
            _output.WriteLine("   " + frame);
        }

        _output.WriteLine($"--- statics NAMED door here: {doorStatics.Count} ---");

        foreach (var door in doorStatics.Take(10))
        {
            _output.WriteLine("   " + door);
        }
    }
}
