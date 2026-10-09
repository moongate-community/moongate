namespace Moongate.Server.Ultima.Types.Pets;

/// <summary>
///     What a pet does with food its owner gives it.
/// </summary>
public enum PetFeedResultType
{
    /// <summary>
    ///     It eats, and is more loyal.
    /// </summary>
    Fed = 0,

    /// <summary>
    ///     It eats, and was as loyal as it can be.
    /// </summary>
    AlreadyHappy = 1,

    /// <summary>
    ///     It does not eat that.
    /// </summary>
    WrongFood = 2,

    /// <summary>
    ///     It is not the player's pet, or not in the world.
    /// </summary>
    NotYours = 3
}
