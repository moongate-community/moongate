using System.Globalization;

namespace Moongate.UO.Data.Signs;

/// <summary>
/// Reads a <see cref="SignEntry.Label" />, which the corpus writes in one of two ways: a cliloc
/// reference such as <c>#1016093</c>, or the words themselves.
/// </summary>
public static class SignLabel
{
    /// <summary>
    /// Splits a label into the cliloc that names it, or the text that does — never both.
    /// <para>
    /// A label that is neither, being blank or a <c>#</c> with nothing usable after it, yields both
    /// empty rather than throwing. This is hand-edited data, and one bad line should cost one sign its
    /// name rather than the whole run.
    /// </para>
    /// </summary>
    public static (int Cliloc, string Text) Split(string? label)
    {
        var trimmed = label?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return (0, string.Empty);
        }

        // Only a leading hash names a cliloc: "Bob's #1 Tavern" is a sign that says so.
        if (trimmed[0] != '#')
        {
            return (0, trimmed);
        }

        // NumberStyles.None rejects a sign or separators, so "#-5" is malformed rather than negative.
        return int.TryParse(trimmed[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var cliloc)
                   ? (cliloc, string.Empty)
                   : (0, string.Empty);
    }
}
