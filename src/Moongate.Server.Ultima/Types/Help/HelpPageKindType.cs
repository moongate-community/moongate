namespace Moongate.Server.Ultima.Types.Help;

/// <summary>
///     What a request for a game master is about.
/// </summary>
public enum HelpPageKindType
{
    /// <summary>
    ///     A question for a game master.
    /// </summary>
    Question = 0,

    /// <summary>
    ///     A bug report.
    /// </summary>
    Bug = 1,

    /// <summary>
    ///     A suggestion.
    /// </summary>
    Suggestion = 2,

    /// <summary>
    ///     A report of harassment.
    /// </summary>
    Harassment = 3
}
