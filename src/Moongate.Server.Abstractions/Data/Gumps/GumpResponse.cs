namespace Moongate.Server.Abstractions.Data.Gumps;

/// <summary>
/// What the player did with a gump: the button pressed, the switches left ticked, and whatever was
/// typed into each text field, keyed by the entry id the gump gave it.
/// </summary>
/// <param name="Button">The pressed button's id; <c>0</c> is the client's own close button.</param>
/// <param name="Switches">The ids of every ticked checkbox and selected radio.</param>
/// <param name="TextEntries">The contents of each text field, keyed by entry id.</param>
public readonly record struct GumpResponse(
    int Button,
    IReadOnlyList<int> Switches,
    IReadOnlyDictionary<int, string> TextEntries
);
