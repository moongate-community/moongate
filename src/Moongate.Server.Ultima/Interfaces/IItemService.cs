using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The live items of the world: what the characters in the world wear, the items lying on the ground, and
///     everything inside those containers, at any depth; the ground items are kept in the sector grid of
///     <see cref="ISectorService" />. The world save writes their snapshots.
/// </summary>
/// <remarks>
///     The live items change on the game loop: add, remove and move them only there. It is also the deletion source of
///     the items absorbed into other stacks: the world save deletes their rows.
/// </remarks>
public interface IItemService : IPersistenceDeletionSource
{
    /// <summary>
    ///     Gets the live items.
    /// </summary>
    IReadOnlyCollection<ItemEntity> Items { get; }

    /// <summary>
    ///     Keeps the items as the live ones; each replaces an instance with the same serial. An item loaded again is no
    ///     longer queued for deletion.
    /// </summary>
    void Add(IEnumerable<ItemEntity> items);

    /// <summary>
    ///     Adds the items loaded for a character entering the world, as its last save left them, leaving out those the
    ///     world has moved on from since: an item already live, which someone else carries or which lies on the ground,
    ///     and an item queued for deletion, merged into a stack.
    /// </summary>
    /// <returns>
    ///     The items added.
    /// </returns>
    IReadOnlyList<ItemEntity> AddLoaded(IEnumerable<ItemEntity> items);

    /// <summary>
    ///     Gets the live item with the serial.
    /// </summary>
    bool TryGet(Serial serial, [NotNullWhen(true)] out ItemEntity? item);

    /// <summary>
    ///     Forgets the items with the serials; unknown serials are skipped.
    /// </summary>
    void Remove(IEnumerable<Serial> serials);

    /// <summary>
    ///     Gets the live items directly inside <paramref name="container" />, in serial order.
    /// </summary>
    IReadOnlyList<ItemEntity> GetContents(Serial container);

    /// <summary>
    ///     Gets the mobile wearing the item, or wearing the outermost container it is in; null for an item on the
    ///     ground or inside a container that is not live.
    /// </summary>
    Serial? GetOwner(ItemEntity item);

    /// <summary>
    ///     Gets the worn item the item is, or is in at any depth, such as the backpack or the bank box; null for an item
    ///     on the ground or inside a container that is not live.
    /// </summary>
    ItemEntity? GetWornRoot(ItemEntity item);

    /// <summary>
    ///     Gets the item on the ground the item is, or is in at any depth, such as a treasure chest; null for an item a
    ///     mobile carries or inside a container that is not live.
    /// </summary>
    ItemEntity? GetGroundRoot(ItemEntity item);

    /// <summary>
    ///     Gets the live items the mobile wears and everything inside them, at any depth.
    /// </summary>
    IReadOnlyList<ItemEntity> GetOwnedBy(Serial mobile);

    /// <summary>
    ///     Gets the live items the mobile wears, from an index kept by wearer.
    /// </summary>
    IReadOnlyList<ItemEntity> GetWorn(Serial mobile);

    /// <summary>
    ///     Puts the live item inside <paramref name="container" /> at <paramref name="position" /> of its gump, in the
    ///     grid slot <paramref name="gridIndex" /> when it is free and otherwise in the next free one.
    /// </summary>
    void MoveToContainer(ItemEntity item, Serial container, Point2D position, int gridIndex = 0);

    /// <summary>
    ///     Lays the live item on the ground of the map, in the sector grid.
    /// </summary>
    void PlaceOnGround(ItemEntity item, MapType map, Point3D location);

    /// <summary>
    ///     Puts a live item on a mobile, on the given layer, taking it out of the sector grid and indexing it as worn.
    /// </summary>
    void Equip(ItemEntity item, Serial mobile, LayerType layer);

    /// <summary>
    ///     Gets whether the mobile can lift the ground item or drop onto it, as ModernUO: same map, within 2 tiles, in line
    ///     of sight.
    /// </summary>
    bool CanReach(MobileEntity mobile, ItemEntity item);

    /// <summary>
    ///     Lays the item on the ground at <paramref name="x" />, <paramref name="y" /> within 2 tiles of the mobile, on the
    ///     highest surface up to 16 above its feet and in line of sight; false leaves the item where it was. The caller
    ///     shows it to the players in range.
    /// </summary>
    bool TryDropOnGround(MobileEntity mobile, ItemEntity item, int x, int y);

    /// <summary>
    ///     Gets whether the item lies on the ground in the sector grid: false while a player holds it.
    /// </summary>
    bool IsLyingOnGround(ItemEntity item);

    /// <summary>
    ///     Records that the character changed an item that no longer belongs to it, such as one it dropped on the ground
    ///     or a ground stack it grew, so its leave saves the item in the same transaction as its own.
    /// </summary>
    void Release(ItemEntity item, Serial owner);

    /// <summary>
    ///     Takes the live items the character released; from then on only the caller saves them.
    /// </summary>
    IReadOnlyList<ItemEntity> TakeReleasedOf(Serial owner);

    /// <summary>
    ///     Takes a ground item out of the sector grid while a player holds it; it keeps its location.
    /// </summary>
    void Hide(ItemEntity item);

    /// <summary>
    ///     Puts a held ground item back into the sector grid where it lies.
    /// </summary>
    void Show(ItemEntity item);

    /// <summary>
    ///     Splits <paramref name="item" />: it keeps its serial and <paramref name="amount" />, and the rest becomes a new
    ///     live item with <paramref name="serial" />, in the same place, with the same template, graphic, hue and props.
    /// </summary>
    /// <returns>
    ///     The rest.
    /// </returns>
    ItemEntity Split(ItemEntity item, int amount, Serial serial);

    /// <summary>
    ///     Forgets the item, merged into another, and queues its row for deletion by the next save.
    /// </summary>
    void Absorb(ItemEntity item);

    /// <summary>
    ///     Forgets the item as <see cref="Absorb(ItemEntity)" /> does, but queues its deletion for
    ///     <paramref name="owner" />: the character that grew the stack, whose leave then deletes the row.
    /// </summary>
    void Absorb(ItemEntity item, Serial owner);

    /// <summary>
    ///     Gets the queued deletions of the items <paramref name="owner" /> carried when they were absorbed.
    /// </summary>
    IReadOnlyCollection<Serial> TombstonesOf(Serial owner);

    /// <summary>
    ///     Hands over the queued deletions of <paramref name="owner" />'s items: the world save no longer deletes them,
    ///     the caller does, together with the stacks they were merged into.
    /// </summary>
    IReadOnlyCollection<Serial> TakeTombstonesOf(Serial owner);
}
