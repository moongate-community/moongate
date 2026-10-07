namespace Moongate.Server.Ultima.Types.BulletinBoards;

/// <summary>
///     What a client asks of a bulletin board with packet 0x71: the sub-command byte. 0x00 to 0x02 are the server's.
/// </summary>
public enum BulletinBoardCommandType : byte
{
    /// <summary>
    ///     The text of a message.
    /// </summary>
    RequestMessage = 0x03,

    /// <summary>
    ///     The summary of a message: who, what about and when.
    /// </summary>
    RequestSummary = 0x04,

    /// <summary>
    ///     A new message, or a reply to one.
    /// </summary>
    Post = 0x05,

    /// <summary>
    ///     The removal of a message.
    /// </summary>
    Remove = 0x06
}
