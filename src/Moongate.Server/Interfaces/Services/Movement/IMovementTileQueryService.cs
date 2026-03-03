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

    /// <summary>
    /// Returns whether an entity can fit at the specified location.
    /// </summary>
    /// <remarks>
    /// Default implementation returns <see langword="true" /> for compatibility with tests/fakes.
    /// </remarks>
    bool CanFit(
        int mapId,
        int x,
        int y,
        int z,
        int height = 16,
        bool checkBlocksFit = false,
        bool checkMobiles = true,
        bool requireSurface = true
    )
        => true;

    /// <summary>
    /// Returns whether an item can fit at the specified location.
    /// </summary>
    /// <remarks>
    /// Item placement differs from movement fit for some surface combinations.
    /// </remarks>
    bool CanFitItem(int mapId, int x, int y, int z, int height = 16)
        => true;
}
