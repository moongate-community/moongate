using Moongate.Persistence.Entities;
using Moongate.UO.Data.Items;

namespace Moongate.Server.Abstractions.Interfaces.Items;

/// <summary>
/// Decides whether an item is one of the things that merge into a pile, which is what
/// <c>DragDropService</c> asks before letting one stack absorb another.
/// </summary>
public interface IStackableRule
{
    /// <summary>
    /// Whether <paramref name="item" /> stacks. <paramref name="template" /> is the item's template, or
    /// null when it no longer resolves.
    /// </summary>
    bool IsStackable(ItemEntity item, ItemTemplate? template);
}
