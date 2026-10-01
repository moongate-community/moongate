namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A built gump: the layout and string table the client is sent, and what the client may answer with.
/// </summary>
public sealed class GumpBuildResult
{
    public required string Layout { get; init; }

    public required IReadOnlyList<string> Strings { get; init; }

    /// <summary>
    ///     Gets the ids of the reply buttons; 0, closing the gump, is always allowed besides them.
    /// </summary>
    public required IReadOnlySet<int> Buttons { get; init; }

    public required IReadOnlySet<int> Switches { get; init; }

    public required IReadOnlySet<int> TextEntries { get; init; }
}
