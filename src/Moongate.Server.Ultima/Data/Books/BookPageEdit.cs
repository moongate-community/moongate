namespace Moongate.Server.Ultima.Data.Books;

/// <summary>
///     One page of a book as the client sends it: its number from 1 and the lines a player wrote on it, or no lines
///     at all when the client only asks for the page.
/// </summary>
/// <param name="Number">
///     The page, from 1.
/// </param>
/// <param name="Lines">
///     What was written, at most eight lines; null for a request of the page.
/// </param>
public sealed record BookPageEdit(int Number, IReadOnlyList<string>? Lines);
