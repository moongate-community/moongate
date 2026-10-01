namespace Moongate.Server.Ultima.Types.Gumps;

/// <summary>
///     Why the server closed a gump the player had not answered.
/// </summary>
public enum GumpCloseReasonType : byte
{
    /// <summary>
    ///     The same gump was opened again on the player.
    /// </summary>
    Replaced,

    /// <summary>
    ///     The server closed it: on request, or to keep the player under the open gumps limit.
    /// </summary>
    Server,

    /// <summary>
    ///     The player's session closed.
    /// </summary>
    Disconnect
}
