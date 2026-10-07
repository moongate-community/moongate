namespace Moongate.Server.Ultima.Types.BulletinBoards;

/// <summary>
///     What came of a post on a bulletin board.
/// </summary>
public enum BulletinPostResultType
{
    /// <summary>
    ///     The message is on the board.
    /// </summary>
    Ok = 0,

    /// <summary>
    ///     It had no subject, or no line of text.
    /// </summary>
    Empty = 1,

    /// <summary>
    ///     The poster posted there too short a time ago.
    /// </summary>
    TooSoon = 2,

    /// <summary>
    ///     No serial was ready for the message: the same post works a moment later.
    /// </summary>
    Busy = 3
}
