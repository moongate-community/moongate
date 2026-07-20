namespace Moongate.Http.Plugin.Data;

/// <summary>A map facet as the API describes it, so a viewer can configure itself rather than guess.</summary>
/// <param name="Name">The facet's name, as used in the tile route.</param>
/// <param name="Width">Facet width in map tiles, which is its width in pixels at native zoom.</param>
/// <param name="Height">Facet height in map tiles.</param>
/// <param name="MaxZoom">The deepest zoom, where one pixel is one map tile. Zoom 0 is always one tile.</param>
/// <param name="TileSize">Tile edge in pixels.</param>
/// <param name="TilesAcross">Tiles across the grid at <paramref name="MaxZoom" />.</param>
/// <param name="TilesDown">Tiles down the grid at <paramref name="MaxZoom" />.</param>
public sealed record MapFacetInfo(
    string Name,
    int Width,
    int Height,
    int MaxZoom,
    int TileSize,
    int TilesAcross,
    int TilesDown
);
