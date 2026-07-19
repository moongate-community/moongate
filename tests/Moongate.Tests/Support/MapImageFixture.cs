using Moongate.Http.Plugin.Interfaces.Ultima;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Io;
using Moongate.Ultima.Maps;
using Moongate.Ultima.Tiles;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Support;

/// <summary>
/// A synthetic client directory with one map block of land, plus a throwaway runtime root for the tile
/// cache. Building it mutates Moongate.Ultima's process-wide statics, so every test using it must sit in
/// the "UltimaClientData" collection.
/// </summary>
public sealed class MapImageFixture : IDisposable
{
    /// <summary>
    /// The facet the stub provider serves, sized so the pyramid is small but real: 1024×512 gives maxZoom
    /// 2, a 4×2 grid at native, 2×1 at z1 and 1×1 at z0 — which means the z0 tile asks for two children
    /// that exist and two that do not, exercising the case that crashes a naive composer.
    /// </summary>
    public const int MapWidth = 1024;

    public const int MapHeight = 512;

    private readonly string _clientDirectory;

    private MapImageFixture(string clientDirectory, string root, DirectoriesConfig directories)
    {
        _clientDirectory = clientDirectory;
        Root = root;
        Directories = directories;
        Provider = new StubMapProvider(MapType.Felucca, new(0, 0, MapWidth, MapHeight));
    }

    public string Root { get; }

    public DirectoriesConfig Directories { get; }

    public IUltimaMapProvider Provider { get; }

    public static MapImageFixture Create()
    {
        // The file must hold every block the facet claims: TileMatrix seeks straight to a block and reads,
        // so a short map0.mul throws EndOfStreamException the moment the renderer passes the first block.
        // 1024x512 tiles is 128x64 blocks, about 1.6 MB of identical land — small enough for a test, and
        // the only shape that makes the renderer work at all.
        var mapBlock = BuildMapFile();
        var radarCol = UltimaFixtures.BuildRadarCol(BuildColors());
        var tileData = UltimaFixtures.BuildTileData();
        var (artIndex, art) = UltimaFixtures.BuildStaticArt(0x10, 2, 2, 0xFFFF);
        var hues = UltimaFixtures.BuildHues("Red", 0x7C00, 0, 0);

        var clientDirectory = UltimaFixtures.CreateClientDirectory(
            ("map0.mul", mapBlock),
            ("radarcol.mul", radarCol),
            ("tiledata.mul", tileData),
            ("artidx.mul", artIndex),
            ("art.mul", art),
            ("hues.mul", hues)
        );

        Files.SetDirectory(clientDirectory);
        Art.Reload();
        TileData.Initialize();
        Hues.Initialize();

        // RadarCol.Initialize is public and re-runnable, which matters: its static constructor already ran
        // against whatever directory the first test to touch it happened to set, and only this call
        // repoints it. RadarColTests does the same.
        RadarCol.Initialize();

        // Deliberately no Map.Reload(): it closes streams on the six *static* facets — Trammel and the
        // rest — whose files this fixture does not create. The facet below is a fresh Map built after
        // SetDirectory, so it reads this directory and owes the statics nothing.

        var root = Path.Combine(Path.GetTempPath(), $"mg_maps_{Guid.NewGuid():N}");

        return new(clientDirectory, root, new(root, []));
    }

    /// <summary>
    /// One land block repeated across the whole facet. The content does not matter — the tests assert
    /// sizes, caching and composition, not pixels — but the file's length does: it must cover every block
    /// the facet spans, or the renderer reads past the end.
    /// </summary>
    private static byte[] BuildMapFile()
    {
        var block = UltimaFixtures.BuildMapBlock(0x0003, 5);
        var blocks = MapWidth / 8 * (MapHeight / 8);
        var file = new byte[block.Length * blocks];

        for (var i = 0; i < blocks; i++)
        {
            block.CopyTo(file, i * block.Length);
        }

        return file;
    }

    private static ushort[] BuildColors()
    {
        // RadarCol is indexed by land and static id, so it must span both ranges: land below 0x4000 and
        // statics above it.
        var colors = new ushort[0x8000];

        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = 0x7C00;
        }

        return colors;
    }

    public void Dispose()
    {
        if (Directory.Exists(_clientDirectory))
        {
            Directory.Delete(_clientDirectory, true);
        }

        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }

}
