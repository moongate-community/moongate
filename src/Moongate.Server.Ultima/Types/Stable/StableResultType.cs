namespace Moongate.Server.Ultima.Types.Stable;

/// <summary>
///     What a stablemaster's service answers when a player stables a pet or claims one back.
/// </summary>
public enum StableResultType
{
    /// <summary>
    ///     It was done.
    /// </summary>
    Ok = 0,

    /// <summary>
    ///     The creature is no mount, or is not a creature of the world: it cannot be stabled.
    /// </summary>
    NotAPet = 1,

    /// <summary>
    ///     The creature is not the player's own.
    /// </summary>
    NotYours = 2,

    /// <summary>
    ///     The creature is too far from the player.
    /// </summary>
    TooFar = 3,

    /// <summary>
    ///     The creature is dying.
    /// </summary>
    Dying = 4,

    /// <summary>
    ///     The stable holds as many pets as it may.
    /// </summary>
    Full = 5,

    /// <summary>
    ///     The player cannot pay the fee, in its backpack or its bank.
    /// </summary>
    NoGold = 6,

    /// <summary>
    ///     The player is an NPC, or is not in the world.
    /// </summary>
    NoPlayer = 7,

    /// <summary>
    ///     The place asked for in the stable holds no such pet.
    /// </summary>
    BadIndex = 8,

    /// <summary>
    ///     The pet could not be taken from the world or its template is gone from the data.
    /// </summary>
    Failed = 9
}
