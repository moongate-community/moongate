using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Pets;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The creatures a player has tamed: how many followers it has, how many it may have, and the making of a creature its
///     own. A creature is a player's when its prop <c>owner</c> is the player's serial.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface IPetService
{
    /// <summary>
    ///     Gets how many followers a player may have.
    /// </summary>
    int MaxFollowers { get; }

    /// <summary>
    ///     Gets how many followers the player has: the slots of the creatures in the world that are its own, and of those that
    ///     are its own and ridden, by it or by a game master; a creature with no entry in the taming data counts for 1. A pet in a stable counts for nothing. The number
    ///     is read from a short memory, so a creature that died a moment ago may count for a moment longer.
    /// </summary>
    int Followers(MobileEntity player);

    /// <summary>
    ///     Tells the service the followers of a player may have changed, a pet stabled, claimed, mounted or dead: the count is
    ///     made again at the next ask, and the player is shown its status at once.
    /// </summary>
    void Changed(Serial player);

    /// <summary>
    ///     Lets <paramref name="creature" /> go: when it is a creature of the world that is the player's own, it has no owner and
    ///     no order any more, and the player's followers are one less. False for anything else.
    /// </summary>
    bool Release(MobileEntity player, MobileEntity creature);

    /// <summary>
    ///     Gets how loyal a creature is to its owner, 0 to 100; 100 for one that has no loyalty set yet.
    /// </summary>
    int Loyalty(MobileEntity creature);

    /// <summary>
    ///     Adds <paramref name="delta" /> to the loyalty of a creature, kept from 0 to 100, and gives the new loyalty.
    /// </summary>
    int AdjustLoyalty(MobileEntity creature, int delta);

    /// <summary>
    ///     Makes a creature wild again by itself, as <see cref="Release" /> does for its owner: no owner, no order, no loyalty,
    ///     and the owner has its followers recounted. False for one that is not an owned creature of the world.
    /// </summary>
    bool LetGo(MobileEntity creature);

    /// <summary>
    ///     Gets the chance, 0 to 1, that <paramref name="creature" /> obeys <paramref name="player" />: always for a creature
    ///     that asks 29.1 of Animal Taming or less and for the staff; else from the Animal Taming and Animal Lore of the
    ///     player against the skill the creature asks, less 1% for each point of loyalty it lacks (a tenth of a point less
    ///     for each point lacking, ModernUO's rule).
    /// </summary>
    double ControlChance(MobileEntity player, MobileEntity creature);

    /// <summary>
    ///     Rolls <see cref="ControlChance" />: an obeying pet gains loyalty; one that does not loses loyalty, and is wild again
    ///     when none is left. Not for the orders that cannot be refused, release.
    /// </summary>
    PetObeyResultType Obey(MobileEntity player, MobileEntity creature);

    /// <summary>
    ///     Gives food to a pet: when it is the player's own and eats that item template, its loyalty rises by the food gain
    ///     for each of <paramref name="amount" /> items. The caller takes the food away.
    /// </summary>
    PetFeedResultType Feed(MobileEntity player, MobileEntity creature, string? itemTemplate, int amount);

    /// <summary>
    ///     Gets whether a creature has bonded with its owner: food from the owner, the right skill and
    ///     <c>ultima.pets.bonding_days</c> of waiting. Only a bonded pet can be raised when it dies.
    /// </summary>
    bool IsBonded(MobileEntity creature);

    /// <summary>
    ///     Gets the slots a creature of that mobile template counts for: its entry in the taming data, else the
    ///     <c>control_slots</c> of the template, else 1.
    /// </summary>
    int SlotsOf(string? templateId);

    /// <summary>
    ///     Makes <paramref name="creature" /> the player's own, when it is a creature of the world with an entry in the taming
    ///     data, has no owner and its slots fit. It does not roll the skill: the skill script does.
    /// </summary>
    PetResultType TryTame(MobileEntity player, MobileEntity creature);
}
