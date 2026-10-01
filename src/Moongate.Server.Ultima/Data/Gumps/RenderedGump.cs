namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A gump of <c>templates/gumps</c> filled for one opening: its layout, where it opens, and the function of the gump
///     script each <c>on_click</c> button calls, by button id.
/// </summary>
public sealed class RenderedGump
{
    public required GumpLayout Layout { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public required IReadOnlyDictionary<int, string> Clicks { get; init; }
}
