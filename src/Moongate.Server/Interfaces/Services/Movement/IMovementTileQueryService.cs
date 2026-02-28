using Moongate.UO.Data.Tiles;

namespace Moongate.Server.Interfaces.Services.Movement;

/// <summary>
/// Reads map/tile data required by movement validation.
/// </summary>
public interface IMovementTileQueryService
{
    /// <summary>
    /// Returns map bounds for the specified map id.
    /// </summary>
    bool TryGetMapBounds(int mapId, out int width, out int height);

    /// <summary>
    /// Returns the land tile at world coordinates.
    /// </summary>
    bool TryGetLandTile(int mapId, int x, int y, out LandTile landTile);

    /// <summary>
    /// Returns static tiles at world coordinates.
    /// </summary>
    IReadOnlyList<StaticTile> GetStaticTiles(int mapId, int x, int y);
}
