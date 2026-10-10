namespace Moongate.Server.Ultima.Types.Mobiles;

/// <summary>
///     What came of poisoning a mobile.
/// </summary>
public enum PoisonResultType : byte
{
    /// <summary>
    ///     The mobile is poisoned at the level given.
    /// </summary>
    Poisoned = 0,

    /// <summary>
    ///     A poison as strong or stronger is at work already: nothing changed.
    /// </summary>
    HigherActive = 1,

    /// <summary>
    ///     An unknown level, or a dead mobile.
    /// </summary>
    Refused = 2
}
