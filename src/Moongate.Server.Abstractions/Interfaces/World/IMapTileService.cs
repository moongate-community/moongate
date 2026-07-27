using Moongate.Persistence.Entities;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>
/// Resolves tile walkability and standing height from the shard's client files: land, statics, and
/// (via the caller-supplied ground items) dynamic obstacles. A port of the classic "CanFit" check,
/// scoped to a single tile — no diagonal-neighbor or other-mobile awareness.
/// </summary>
public interface IMapTileService
{
    /// <summary>
    /// Finds the Z a mobile currently at <paramref name="currentZ" /> could stand at on tile
    /// (<paramref name="x" />, <paramref name="y" />) of <paramref name="mapId" />, preferring the
    /// candidate closest to <paramref name="currentZ" />. <paramref name="groundItems" /> are dynamic
    /// items to consider as obstacles/surfaces, treated identically to statics — items whose position
    /// is not exactly (<paramref name="x" />, <paramref name="y" />) are ignored, so callers may pass
    /// a wider sweep (e.g. every neighboring tile) without pre-filtering to the exact target.
    /// </summary>
    bool TryGetWalkableZ(int mapId, int x, int y, int currentZ, IReadOnlyList<ItemEntity> groundItems, out int newZ);
}
