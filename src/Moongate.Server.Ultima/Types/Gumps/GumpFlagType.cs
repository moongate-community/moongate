namespace Moongate.Server.Ultima.Types.Gumps;

/// <summary>
///     A flag of a whole gump.
/// </summary>
public enum GumpFlagType : byte
{
    /// <summary>
    ///     The player cannot drag it.
    /// </summary>
    NoMove,

    /// <summary>
    ///     A right click does not close it.
    /// </summary>
    NoClose,

    /// <summary>
    ///     Escape does not close it.
    /// </summary>
    NoDispose,

    /// <summary>
    ///     The player cannot resize it.
    /// </summary>
    NoResize
}
