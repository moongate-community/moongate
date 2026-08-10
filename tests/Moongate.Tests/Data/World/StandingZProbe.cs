using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Maps;
using Moongate.Ultima.Tiles;
using Xunit.Abstractions;

namespace Moongate.Tests.Data.World;

/// <summary>A probe: what height the world offers where the player stands. Skips without client files.</summary>
public class StandingZProbe
{
    private const int PlayerX = 3491;
    private const int PlayerY = 2571;
    private const int ServerZ = 22;

    private readonly ITestOutputHelper _output;

    public StandingZProbe(ITestOutputHelper output)
    {
        _output = output;
    }

    [ClientFilesFact]
    public void ReportTheHeightsHere()
    {
        Files.SetDirectory(ClientFiles.Directory);
        TileData.Initialize();

        var map = Map.Trammel;

        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var x = PlayerX + dx;
                var y = PlayerY + dy;
                var land = map.Tiles.GetLandTile(x, y);
                var statics = map.Tiles.GetStaticTiles(x, y);
                var tops = string.Join(
                    ", ",
                    statics.Select(t => $"0x{t.Id:X4}@{t.Z}+{TileData.ItemTable[t.Id].Height}")
                );

                _output.WriteLine(
                    $"{x},{y}  land z={land.Z}  statics[{statics.Length}] {tops}"
                );
            }
        }

        _output.WriteLine($"--- the server believes the player is at z={ServerZ} ---");
    }
}
