using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Splits every map into 16×16 sectors and keeps the live mobiles and the items on the ground in them, so the ones
///     near a point are found without scanning the world.
/// </summary>
/// <remarks>
///     Game loop only. A mobile outside its map, or on a map without <c>maps.toml</c> content, is kept out of the grid.
/// </remarks>
public interface ISectorService
{
    /// <summary>
    ///     Puts the mobile in the sector of its location.
    /// </summary>
    void Add(MobileEntity mobile);

    /// <summary>
    ///     Takes the mobile out of its sector; nothing happens when it is not in one.
    /// </summary>
    void Remove(MobileEntity mobile);

    /// <summary>
    ///     Moves the mobile to the sector of its current location when it changed.
    /// </summary>
    void Move(MobileEntity mobile);

    /// <summary>
    ///     Gets the mobiles on the map within <paramref name="range" /> tiles of the center on both axes.
    /// </summary>
    IReadOnlyList<MobileEntity> GetMobilesInRange(MapType map, Point3D center, int range);

    /// <summary>
    ///     Puts a ground item in the sector of its location; an item not on the ground is ignored.
    /// </summary>
    void AddItem(ItemEntity item);

    /// <summary>
    ///     Takes the item out of its sector; nothing happens when it is not in one.
    /// </summary>
    void RemoveItem(ItemEntity item);

    /// <summary>
    ///     Gets the ground items on the map within <paramref name="range" /> tiles of the center on both axes.
    /// </summary>
    IReadOnlyList<ItemEntity> GetItemsInRange(MapType map, Point3D center, int range);
}
