using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The live items of the characters in the world: what they wear and everything inside those containers, at any
///     depth. The world save writes their snapshots.
/// </summary>
/// <remarks>
///     The live items change on the game loop: add, remove and move them only there.
/// </remarks>
public interface IItemService
{
    /// <summary>
    ///     Gets the live items.
    /// </summary>
    IReadOnlyCollection<ItemEntity> Items { get; }

    /// <summary>
    ///     Keeps the items as the live ones; each replaces an instance with the same serial.
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
}
