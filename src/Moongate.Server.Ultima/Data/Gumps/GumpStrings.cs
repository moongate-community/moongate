namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     The string table of a gump: each text once, by index, as the layout refers to it.
/// </summary>
public sealed class GumpStrings
{
    private readonly Dictionary<string, int> _indexes = new(StringComparer.Ordinal);
    private readonly List<string> _strings = [];

    public IReadOnlyList<string> Strings => _strings;

    /// <summary>
    ///     Gets the index of <paramref name="text" />, cut to the 65535 characters a string of the table can hold.
    /// </summary>
    public int Intern(string text)
    {
        if (text.Length > ushort.MaxValue)
        {
            text = text[..ushort.MaxValue];
        }

        if (!_indexes.TryGetValue(text, out var index))
        {
            index = _strings.Count;
            _indexes[text] = index;
            _strings.Add(text);
        }

        return index;
    }
}
