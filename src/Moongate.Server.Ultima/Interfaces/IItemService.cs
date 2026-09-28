using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The live items of the characters in the world: what they wear and everything inside those containers, at any
///     depth. The world save writes their snapshots.
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
    ///     Gets the live items the mobile wears and everything inside them, at any depth.
    /// </summary>
    IReadOnlyList<ItemEntity> GetOwnedBy(Serial mobile);

    /// <summary>
    ///     Puts the live item inside <paramref name="container" /> at <paramref name="position" /> of its gump.
    /// </summary>
    void MoveToContainer(ItemEntity item, Serial container, Point2D position);

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
    ///     Gets the queued deletions of the items <paramref name="owner" /> carried when they were absorbed.
    /// </summary>
    IReadOnlyCollection<Serial> TombstonesOf(Serial owner);

    /// <summary>
    ///     Hands over the queued deletions of <paramref name="owner" />'s items: the world save no longer deletes them,
    ///     the caller does, together with the stacks they were merged into.
    /// </summary>
    IReadOnlyCollection<Serial> TakeTombstonesOf(Serial owner);
}
