namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A player's answer to a gump, already checked against what the gump offered: the button (0 when closed), the
///     switches on and the text of each text entry.
/// </summary>
public sealed class GumpResponse
{
    public required int ButtonId { get; init; }

    public required IReadOnlySet<int> Switches { get; init; }

    public required IReadOnlyDictionary<int, string> Texts { get; init; }
}
