namespace Moongate.Server.Ultima.Types.Movement;

/// <summary>
///     What happened when a mobile was asked to move one way.
/// </summary>
public enum MoveResultType : byte
{
    /// <summary>
    ///     It faced another way: it turned and stayed on its cell.
    /// </summary>
    Turned,

    /// <summary>
    ///     It stepped to the next cell.
    /// </summary>
    Moved,

    /// <summary>
    ///     The step was not allowed: it stayed where it was.
    /// </summary>
    Blocked
}
