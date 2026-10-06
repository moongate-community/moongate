namespace Moongate.Server.Services.Console.Internal;

/// <summary>
///     Completes the word being typed on the console line, as TAB does in a shell: the command name, then its arguments.
/// </summary>
internal static class ConsoleCompletion
{
    /// <summary>
    ///     Completes the last word of <paramref name="line" />, empty after a space, among the values
    ///     <paramref name="candidates" /> gives for the words before it (none for the command name), in any case: one match
    ///     gives the value and a space, several give their common prefix.
    /// </summary>
    /// <returns>
    ///     The completed line, and the values that matched in order.
    /// </returns>
    public static (string Text, IReadOnlyList<string> Matches) Complete(
        string line,
        Func<IReadOnlyList<string>, IEnumerable<string>> candidates
    )
    {
        // What comes before the word being typed is kept as it is, spaces included: the command parser skips them.
        var start = line.Length;

        while (start > 0 && !char.IsWhiteSpace(line[start - 1]))
        {
            start--;
        }

        var head = line[..start];
        var word = line[start..];
        var previous = head.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var matches = candidates(previous)
            .Where(value => value.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToList();

        return matches.Count switch
        {
            0 => (line, matches),
            1 => (head + matches[0] + " ", matches),
            _ => (head + CommonPrefix(matches), matches)
        };
    }

    // In the case of the values: "SA" with save and saveall gives "save".
    private static string CommonPrefix(List<string> matches)
    {
        var length = matches[0].Length;

        foreach (var match in matches)
        {
            var same = 0;

            while (same < length && same < match.Length &&
                   char.ToLowerInvariant(match[same]) == char.ToLowerInvariant(matches[0][same]))
            {
                same++;
            }

            length = same;
        }

        return matches[0][..length];
    }
}
