namespace Moongate.Server.Ultima.Data.Tooltips;

/// <summary>
///     One tooltip line: a cliloc number of the client's string table and its arguments, tab-separated; an argument
///     written <c>#1234</c> is the text of cliloc 1234 in the client's language.
/// </summary>
public sealed record PropertyEntry(int Cliloc, string Arguments);
