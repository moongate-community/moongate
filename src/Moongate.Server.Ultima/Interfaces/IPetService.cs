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
    ///     Gets the slots a creature of that mobile template counts for; 1 for one with no entry.
    /// </summary>
    int SlotsOf(string? templateId);

    /// <summary>
    ///     Makes <paramref name="creature" /> the player's own, when it is a creature of the world with an entry in the taming
    ///     data, has no owner and its slots fit. It does not roll the skill: the skill script does.
    /// </summary>
    PetResultType TryTame(MobileEntity player, MobileEntity creature);
}
