namespace Moongate.Server.Ultima.Types.Pets;

/// <summary>
///     What a pet does with an order of its owner.
/// </summary>
public enum PetObeyResultType
{
    /// <summary>
    ///     It obeys.
    /// </summary>
    Obeyed = 0,

    /// <summary>
    ///     It does not: it is angry, and less loyal.
    /// </summary>
    Disobeyed = 1,

    /// <summary>
    ///     It does not, and it had no loyalty left: it is wild again.
    /// </summary>
    Wild = 2,

    /// <summary>
    ///     It is not the player's pet, or not in the world.
    /// </summary>
    NotYours = 3
}
