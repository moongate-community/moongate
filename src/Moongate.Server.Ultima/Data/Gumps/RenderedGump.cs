namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A gump of <c>templates/gumps</c> filled for one opening: its layout, where it opens, the function of the gump
///     script each <c>on_click</c> button calls, the gump each <c>open</c> button opens, and the controls with
///     <c>bind</c>.
/// </summary>
public sealed class RenderedGump
{
    public required GumpLayout Layout { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public required IReadOnlyDictionary<int, string> Clicks { get; init; }

    /// <summary>
    ///     Gets the gump each <c>open</c> button opens, by button id.
    /// </summary>
    public required IReadOnlyDictionary<int, string> Opens { get; init; }

    public required IReadOnlyList<GumpBind> Binds { get; init; }
}
