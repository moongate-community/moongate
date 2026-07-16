namespace Moongate.Network.Data;

/// <summary>
/// One line of an object property list: a cliloc number plus its arguments. Multi-slot arguments
/// (<c>~1_...~ ~2_...~</c>) are separated by a TAB; an empty string means the cliloc has no argument.
/// </summary>
public readonly record struct OplEntry(int Cliloc, string Arguments);
