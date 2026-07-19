using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>
/// Sector-grid spatial index over world entities (16×16-tile buckets). Holds serials only and
/// resolves entities through the persistence stores at query time, so results are detached clones
/// like every other store read. Loop-affine: call it from the game-loop thread.
/// </summary>
public interface ISpatialIndexService
{
    /// <summary>
    /// Indexes <paramref name="mobile" /> at its current <c>MapId</c>/<c>Position</c>, relocating
    /// it from wherever it was indexed before. Persist position changes before calling.
    /// </summary>
    void AddOrUpdate(MobileEntity mobile);

    /// <summary>
    /// Indexes a ground item at its current <c>MapId</c>/<c>Position</c>. An item that is in a
    /// container or equipped is removed from the index instead, so callers can invoke this after
    /// any item write without checking where the item ended up.
    /// </summary>
    void AddOrUpdate(ItemEntity item);

    /// <summary>Ground items on <paramref name="mapId" /> within <paramref name="range" /> tiles of <paramref name="center" />.</summary>
    IReadOnlyList<ItemEntity> GetItemsInRange(int mapId, Point3D center, int range);

    /// <summary>Mobiles on <paramref name="mapId" /> within <paramref name="range" /> tiles of <paramref name="center" />.</summary>
    IReadOnlyList<MobileEntity> GetMobilesInRange(int mapId, Point3D center, int range);

    /// <summary>Removes an entity from the index; unknown serials are a no-op.</summary>
    void Remove(Serial serial);
}
