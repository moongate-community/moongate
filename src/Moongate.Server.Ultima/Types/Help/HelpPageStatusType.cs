namespace Moongate.Server.Ultima.Types.Help;

/// <summary>
///     Where a request for a game master stands.
/// </summary>
public enum HelpPageStatusType
{
    /// <summary>
    ///     Nobody has taken it yet.
    /// </summary>
    Open = 0,

    /// <summary>
    ///     A game master took it.
    /// </summary>
    Taken = 1,

    /// <summary>
    ///     It was answered or closed.
    /// </summary>
    Closed = 2
}
