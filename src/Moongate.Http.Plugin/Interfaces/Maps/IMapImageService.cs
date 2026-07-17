using Moongate.Http.Plugin.Types;
using Moongate.UO.Data.Types;

namespace Moongate.Http.Plugin.Interfaces.Maps;

/// <summary>Map facets as slippy-map tiles and as one whole-facet image, cached on disk.</summary>
public interface IMapImageService
{
    /// <summary>
    /// False when the UO client files are not loaded and nothing can be rendered. Keeps a shard with a
    /// wrong UltimaDirectory apart from a tile that is simply outside the grid.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// The zoom at which one pixel is one map tile for this facet, and so the deepest zoom it serves. Zoom
    /// 0 is always a single tile holding the whole facet. -1 when the facet is not served.
    /// </summary>
    int MaxZoomFor(MapType facet);

    /// <summary>
    /// The path of the cached tile in the given <paramref name="style" />, building it — and, below native
    /// zoom, its children — on first request. Null when the facet is not served or the tile is outside that
    /// zoom's grid.
    /// </summary>
    Task<string?> GetTileAsync(
        MapType facet,
        MapRenderStyleType style,
        int zoom,
        int x,
        int y,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// The path of the cached whole-facet image in the given <paramref name="style" />, rendering it on
    /// first request. Null when the facet is not served. This one is expensive — a facet-sized bitmap —
    /// which is what the pre-warm is for.
    /// </summary>
    Task<string?> GetFullAsync(
        MapType facet,
        MapRenderStyleType style,
        CancellationToken cancellationToken = default
    );
}
