using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     How many items a container holds: the <c>max_items</c> of its template, and for a bank box the setting
///     <c>ultima.bank.max_items</c>. Items are counted with what is inside the bags, a pile as one item.
/// </summary>
public interface IContainerCapacityService
{
    /// <summary>
    ///     The client text a player reads when a container is full: "That container cannot hold more items."
    /// </summary>
    const int FullMessage = 1080017;

    /// <summary>
    ///     Gets the most items the container holds; null when it has no limit.
    /// </summary>
    int? MaximumOf(ItemEntity container);

    /// <summary>
    ///     Gets how many items are inside the container, at any depth.
    /// </summary>
    int CountIn(ItemEntity container);

    /// <summary>
    ///     Gets whether the item, with what is inside it, fits in the container and in every container around it. A
    ///     container the item is already inside gets no fuller.
    /// </summary>
    bool HasRoom(ItemEntity container, ItemEntity item);

    /// <summary>
    ///     Gets whether that many new items fit in the container and in every container around it.
    /// </summary>
    bool HasRoomFor(ItemEntity container, int items);
}
