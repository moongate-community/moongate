namespace Moongate.Server.Ultima.Types.Death;

/// <summary>
///     What came of raising a corpse.
/// </summary>
public enum ResurrectResultType
{
    /// <summary>
    ///     Who died is back in the world.
    /// </summary>
    Raised,

    /// <summary>
    ///     The serial is not a corpse lying on the ground.
    /// </summary>
    NotACorpse,

    /// <summary>
    ///     The corpse does not say which mobile template to raise, or names one that no longer exists, or is being
    ///     raised already.
    /// </summary>
    CannotBeRaised
}
