using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     What things weigh, in whole stones: a pile, a container with what is in it, what a mobile carries and how much
///     it may. Counted when asked, from the items as they are. Called on the game loop.
/// </summary>
public interface IWeightService
{
    /// <summary>
    ///     The stones of the container the template of a container does not limit.
    /// </summary>
    const int DefaultContainerMaximum = 400;

    /// <summary>
    ///     Gets the weight of the item with everything inside it: each pile is its unit weight times its amount,
    ///     rounded up, as its tooltip says.
    /// </summary>
    int Of(ItemEntity item);

    /// <summary>
    ///     Gets what the mobile carries: what it wears with their contents, the bank box left out, and the item its
    ///     player holds on the cursor.
    /// </summary>
    int Carried(MobileEntity mobile);

    /// <summary>
    ///     Gets how much the mobile carries without being overloaded: 40 stones and three and a half a point of
    ///     strength, as the other emulators.
    /// </summary>
    int MaxCarried(MobileEntity mobile);

    /// <summary>
    ///     Gets whether the container, and every container it is in, still holds its limit of stones with the item in
    ///     it. The limit is the template's <c>max_weight</c>, 400 without one and none for 0; a bank box has none, though
    ///     a container inside it keeps its own. A container the item is already in is not asked.
    /// </summary>
    bool Holds(ItemEntity container, ItemEntity item);

    /// <summary>Checks the cumulative weight of a new detached batch against every ancestor.</summary>
    bool Holds(ItemEntity container, IReadOnlyList<ItemEntity> additions);
}
