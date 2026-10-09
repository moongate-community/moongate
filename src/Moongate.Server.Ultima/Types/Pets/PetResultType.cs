namespace Moongate.Server.Ultima.Types.Pets;

/// <summary>
///     What a try to make a creature a player's own answers.
/// </summary>
public enum PetResultType
{
    /// <summary>
    ///     The creature is the player's now.
    /// </summary>
    Ok = 0,

    /// <summary>
    ///     The target is no creature of the world: an item, a player, someone gone.
    /// </summary>
    NotAnNpc = 1,

    /// <summary>
    ///     The creature has no entry in <c>data/taming.toml</c>.
    /// </summary>
    NotTamable = 2,

    /// <summary>
    ///     The creature has an owner already.
    /// </summary>
    AlreadyOwned = 3,

    /// <summary>
    ///     The creature's slots do not fit in the player's followers.
    /// </summary>
    TooManyFollowers = 4,

    /// <summary>
    ///     The player is an NPC, or is not in the world.
    /// </summary>
    NoPlayer = 5
}
