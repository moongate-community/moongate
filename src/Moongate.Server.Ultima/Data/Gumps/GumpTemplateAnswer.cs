namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A player's answer to a gump of <c>templates/gumps</c>: the checked answer, and the <c>on_click</c> name of the
///     button pressed, null for an <c>id</c> button or when the player closed it.
/// </summary>
public sealed class GumpTemplateAnswer
{
    public required GumpResponse Response { get; init; }

    public string? Click { get; init; }

    /// <summary>
    ///     Gets the gump the pressed <c>open</c> button opens, null for any other button.
    /// </summary>
    public string? Open { get; init; }

    /// <summary>
    ///     Gets the answers of the controls with <c>bind</c>, by name: a text as a string, a checkbox as a bool, a radio
    ///     group as the long switch id of the radio on (absent when none is).
    /// </summary>
    public required IReadOnlyDictionary<string, object> Bound { get; init; }
}
