using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Stable;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The stable of a player: the pets it leaves with a stablemaster and takes back. A stabled pet is only its template
///     id, kept in a prop of the player and saved with it, so nothing of the pet is left in the world; the pet is made
///     again from its template, with its owner, when it is claimed.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface IStableService
{
    /// <summary>
    ///     Gets the template ids of the pets the player has in the stable, in the order they were left.
    /// </summary>
    IReadOnlyList<string> Stabled(MobileEntity player);

    /// <summary>
    ///     Leaves <paramref name="pet" /> in the stable of <paramref name="player" />: the creature leaves the world, its
    ///     template joins the list and the fee is paid from the backpack and then the bank. Refused, and nothing paid,
    ///     when the creature is no mount (<see cref="StableResultType.NotAPet" />), is not the player's
    ///     (<see cref="StableResultType.NotYours" />), is more than a tile away, dying, when the stable is full or when the
    ///     player cannot pay the fee.
    /// </summary>
    StableResultType TryStable(MobileEntity player, MobileEntity pet);

    /// <summary>
    ///     Takes the pet at place <paramref name="index" /> of the list, a template id that must be
    ///     <paramref name="template" />, out of the stable and makes it again on the tile of the player, with the player as
    ///     its owner, off the game loop; a spawn that fails is tried three times and logged. The entry is gone from the list
    ///     at once, so a second claim finds the list shorter. A template that is gone from the data is dropped from the
    ///     list and answers <see cref="StableResultType.Failed" />.
    /// </summary>
    StableResultType TryClaim(MobileEntity player, int index, string template);
}
