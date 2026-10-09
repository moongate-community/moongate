using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Makes, gives, consumes and deletes live items and tells the clients, for the code that changes what a mobile
///     carries: the <c>item</c> Lua module and the services that hand out or take items, such as the jail.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface IItemHandlingService
{
    /// <summary>
    ///     Makes an item from a template with a serial of the reserved pool; it is not in the world yet. Null for an
    ///     unknown template or when no serial is left; a serial is taken only when the item can be made.
    /// </summary>
    ItemEntity? Make(string template, int? amount = null);

    /// <summary>
    ///     Makes an item from a template in the backpack of <paramref name="owner" /> and shows it to it. What stacks
    ///     joins the stack of its kind already lying in the backpack, as a player's drop onto it would (same template,
    ///     graphic, hue, name and rarity, no prop on either, 60000 at most): that stack is what is returned, and no
    ///     slot or serial is used. Null, with no serial used, for a mobile without a backpack, a backpack with no room
    ///     for another item, and as <see cref="Make" />.
    /// </summary>
    ItemEntity? Give(MobileEntity owner, string template, int? amount = null);

    /// <summary>
    ///     Takes <paramref name="amount" /> units off the item, deleting it at 0, and shows the change. False for an
    ///     amount below 1 or above what it has, a worn item or one held on a cursor.
    /// </summary>
    bool Consume(ItemEntity item, int amount = 1);

    /// <summary>
    ///     Deletes the item and takes it off the screens. False for a worn item, one held on a cursor or a container
    ///     that still holds items.
    /// </summary>
    bool Delete(ItemEntity item);

    /// <summary>
    ///     Shows the item again as it is now: to the players around a ground item, to the owner of a carried one.
    /// </summary>
    void Refresh(ItemEntity item);

    /// <summary>
    ///     Gets whether the item is on a player's cursor: it keeps the place it was lifted from until it is dropped.
    /// </summary>
    bool IsHeld(ItemEntity item);
}
