using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Interfaces.Maps;
using Moongate.Http.Plugin.Interfaces.Ultima;
using Moongate.Http.Plugin.Types;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Ultima.Imaging;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SquidStd.Core.Directories;
using UltimaMap = Moongate.Ultima.Maps.Map;

namespace Moongate.Http.Plugin.Services.Maps;

/// <summary>
/// Renders map facets into cached PNG tiles. The pyramid is built from the bottom: a native tile is one
/// GetImage of exactly 32 blocks, and every zoom below it is composed by halving its four children. The
/// alternative — rendering a low zoom directly — means a facet-sized bitmap, about 100 MB for Felucca,
/// inside a request.
/// </summary>
public sealed class MapImageService : IMapImageService
{
    private const string CacheDirectory = "cache/images/maps";

    private readonly IUltimaMapProvider _maps;
    private readonly IUltimaReadGate _gate;
    private readonly string _cachePath;

    public MapImageService(IUltimaMapProvider maps, DirectoriesConfig directories, IUltimaReadGate gate)
    {
        _maps = maps;
        _gate = gate;
        _cachePath = directories.RegisterDirectory(CacheDirectory);

        // Relief shading is driven by process-wide statics that invalidate a per-Map altitude cache, so
        // they must be fixed once, never per request. Sharp, not the client-faithful Soft: Soft's contrast
        // is about a 1% brightness swing — real but imperceptible on screen — so the relief style would
        // look no different from flat. Sharp makes hills and valleys actually read. Flat terrain has no
        // slope and so no shading either way, so this only affects the parts worth seeing.
        UltimaMap.ShadingPreset = AltitudeShadingPresetType.Sharp;
    }

    public bool IsReady => TileData.ItemTable is not null;

    public async Task<string?> GetFullAsync(
        MapType facet,
        MapRenderStyleType style,
        CancellationToken cancellationToken = default
    )
    {
        if (_maps.Get(facet) is not { } map)
        {
            return null;
        }

        var path = Path.Combine(StyleDirectory(facet, style), "full.png");

        if (File.Exists(path))
        {
            return path;
        }

        // A facet-sized bitmap, unavoidably: the output is that image. The gate makes concurrent first
        // requests queue rather than each allocating their own, and the re-check means only the first pays.
        using var image = await _gate.ReadAsync(
            () =>
            {
                if (File.Exists(path))
                {
                    return null;
                }

                using var bitmap = RenderBlocks(map, style, 0, 0, Blocks(map.Width), Blocks(map.Height));

                return bitmap.ToImage();
            },
            cancellationToken
        );

        if (image is null)
        {
            return File.Exists(path) ? path : null;
        }

        // GetImage rounds up to whole blocks, so trim back to the facet's real extent.
        if (image.Width != map.Width || image.Height != map.Height)
        {
            image.Mutate(context => context.Crop(new(0, 0, map.Width, map.Height)));
        }

        await WriteAtomicallyAsync(path, image, cancellationToken);

        return path;
    }

    public async Task<string?> GetTileAsync(
        MapType facet,
        MapRenderStyleType style,
        int zoom,
        int x,
        int y,
        CancellationToken cancellationToken = default
    )
    {
        if (_maps.Get(facet) is not { } map)
        {
            return null;
        }

        var maxZoom = MapTileGeometry.MaxZoom(map.Width, map.Height);

        if (zoom < 0 || zoom > maxZoom || !InGrid(map, zoom, maxZoom, x, y))
        {
            return null;
        }

        var path = TilePath(facet, style, zoom, x, y);

        // The hit path touches neither the gate nor Ultima. Everything below is the miss.
        if (File.Exists(path))
        {
            return path;
        }

        using var tile = zoom == maxZoom
            ? await RenderNativeAsync(map, style, x, y, cancellationToken)
            : await ComposeAsync(facet, style, zoom, x, y, cancellationToken);

        await WriteAtomicallyAsync(path, tile, cancellationToken);

        return path;
    }

    public int MaxZoomFor(MapType facet)
        => _maps.Get(facet) is { } map ? MapTileGeometry.MaxZoom(map.Width, map.Height) : -1;

    /// <summary>Map extent in whole 8×8 blocks, rounded up: a partial block still holds tiles.</summary>
    private static int Blocks(int extent)
        => (extent + 7) / 8;

    /// <summary>
    /// Halves the four children into one tile. Fewer than four is normal: the grids do not double cleanly,
    /// because each level's size is its own ceil — Felucca's z1 is 2×1, so its z0 tile asks for two
    /// children that do not exist. A missing child leaves its quadrant transparent; treating it as a
    /// failure would make zoom 0 unreachable on every non-square facet, which is all of them.
    /// </summary>
    private async Task<Image<Bgra32>> ComposeAsync(
        MapType facet,
        MapRenderStyleType style,
        int zoom,
        int x,
        int y,
        CancellationToken cancellationToken
    )
    {
        var tile = new Image<Bgra32>(MapTileGeometry.TileSize, MapTileGeometry.TileSize);
        var half = MapTileGeometry.TileSize / 2;

        foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
        {
            var childPath = await GetTileAsync(facet, style, zoom + 1, x * 2 + dx, y * 2 + dy, cancellationToken);

            if (childPath is null)
            {
                continue;
            }

            using var child = await Image.LoadAsync<Bgra32>(childPath, cancellationToken);
            child.Mutate(context => context.Resize(half, half));
            tile.Mutate(context => context.DrawImage(child, new Point(dx * half, dy * half), 1f));
        }

        return tile;
    }

    private static bool InGrid(UltimaMap map, int zoom, int maxZoom, int x, int y)
        => x >= 0 &&
           y >= 0 &&
           x < MapTileGeometry.TilesAcross(map.Width, zoom, maxZoom) &&
           y < MapTileGeometry.TilesDown(map.Height, zoom, maxZoom);

    /// <summary>
    /// The style's block render. Flat is the radar map; Relief is the same map with altitude shading,
    /// which only differs at native zoom — every lower zoom is composed by downsampling native tiles.
    /// </summary>
    private static UltimaBitmap RenderBlocks(
        UltimaMap map,
        MapRenderStyleType style,
        int x,
        int y,
        int width,
        int height
    )
        => style == MapRenderStyleType.Relief
            ? map.GetImageWithAltitude(x, y, width, height, true, MapAltitudeModeType.NormalWithAltitude)
            : map.GetImage(x, y, width, height, true);

    /// <summary>
    /// One GetImage of exactly 32 blocks, clipped at the facet's edge. The remainder of an edge tile is
    /// left transparent: the grid rounds up, so the last tile is usually partial, and a viewer clips it.
    /// </summary>
    private async Task<Image<Bgra32>> RenderNativeAsync(
        UltimaMap map,
        MapRenderStyleType style,
        int x,
        int y,
        CancellationToken cancellationToken
    )
    {
        var blockX = x * MapTileGeometry.BlocksPerTile;
        var blockY = y * MapTileGeometry.BlocksPerTile;
        var width = Math.Min(MapTileGeometry.BlocksPerTile, Blocks(map.Width) - blockX);
        var height = Math.Min(MapTileGeometry.BlocksPerTile, Blocks(map.Height) - blockY);

        var rendered = await _gate.ReadAsync(
            () =>
            {
                using var bitmap = RenderBlocks(map, style, blockX, blockY, width, height);

                return bitmap.ToImage();
            },
            cancellationToken
        );

        if (rendered.Width == MapTileGeometry.TileSize && rendered.Height == MapTileGeometry.TileSize)
        {
            return rendered;
        }

        using (rendered)
        {
            var tile = new Image<Bgra32>(MapTileGeometry.TileSize, MapTileGeometry.TileSize);
            tile.Mutate(context => context.DrawImage(rendered, new Point(0, 0), 1f));

            return tile;
        }
    }

    private string StyleDirectory(MapType facet, MapRenderStyleType style)
        => Directory.CreateDirectory(
                Path.Combine(_cachePath, facet.ToString().ToLowerInvariant(), style.ToString().ToLowerInvariant())
            )
            .FullName;

    private string TilePath(MapType facet, MapRenderStyleType style, int zoom, int x, int y)
    {
        var directory = Directory.CreateDirectory(Path.Combine(StyleDirectory(facet, style), zoom.ToString(), x.ToString()));

        return Path.Combine(directory.FullName, $"{y}.png");
    }

    /// <summary>
    /// Writes through a temporary file in the same directory, then moves it into place. A reader must never
    /// be handed a half-written PNG, and a move within one directory is atomic on Linux and Windows alike —
    /// writing straight to the final path would not be.
    /// </summary>
    private static async Task WriteAtomicallyAsync(
        string path,
        Image<Bgra32> image,
        CancellationToken cancellationToken
    )
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await image.SaveAsPngAsync(temporary, cancellationToken);

            File.Move(temporary, path, true);
        }
        catch
        {
            File.Delete(temporary);

            throw;
        }
    }
}
