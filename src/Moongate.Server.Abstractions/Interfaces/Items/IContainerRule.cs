using Moongate.Persistence.Entities;
using Moongate.UO.Data.Items;

namespace Moongate.Server.Abstractions.Interfaces.Items;

/// <summary>
/// Decides whether an item can hold other items. The drop path needs this to tell the two gestures the
/// 0x08 packet cannot: releasing something into a bag, and releasing it onto whatever is drawn there.
/// </summary>
public interface IContainerRule
{
    /// <summary>True when <paramref name="item" /> is something a player can put things inside.</summary>
    /// <param name="item">The item being dropped onto.</param>
    /// <param name="template">Its template, consulted only when the client tables cannot answer.</param>
    bool IsContainer(ItemEntity item, ItemTemplate? template);
}
