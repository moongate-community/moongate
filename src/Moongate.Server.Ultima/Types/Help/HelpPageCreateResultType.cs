namespace Moongate.Server.Ultima.Types.Help;

/// <summary>
///     What the queue answered to a player who asks for a game master.
/// </summary>
public enum HelpPageCreateResultType
{
    /// <summary>
    ///     The request was accepted.
    /// </summary>
    Ok = 0,

    /// <summary>
    ///     The player already has a request open or taken.
    /// </summary>
    AlreadyOpen = 1,

    /// <summary>
    ///     The player asked too soon after its last request.
    /// </summary>
    Wait = 2,

    /// <summary>
    ///     The text is empty.
    /// </summary>
    BadText = 3
}
