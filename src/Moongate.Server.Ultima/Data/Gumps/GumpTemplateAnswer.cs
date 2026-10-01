namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A player's answer to a gump of <c>templates/gumps</c>: the checked answer, and the <c>on_click</c> name of the
///     button pressed, null for an <c>id</c> button or when the player closed it.
/// </summary>
public sealed class GumpTemplateAnswer
{
    public required GumpResponse Response { get; init; }

    public string? Click { get; init; }
}
