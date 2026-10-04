namespace Moongate.Server.Services.Console.Internal;

/// <summary>
///     Completes the command name the console line starts with, as TAB does in a shell.
/// </summary>
internal static class ConsoleCompletion
{
    /// <summary>
    ///     Completes the first word of <paramref name="line" /> among <paramref name="names" />, in any case: one match
    ///     gives the name and a space, several give their common prefix. Past the first word nothing is completed.
    /// </summary>
    /// <returns>The completed line, and the names that matched in order; none past the first word.</returns>
    public static (string Text, IReadOnlyList<string> Matches) Complete(string line, IEnumerable<string> names)
    {
        if (line.Any(char.IsWhiteSpace))
        {
            return (line, []);
        }

        var matches = names.Where(name => name.StartsWith(line, StringComparison.OrdinalIgnoreCase))
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .Order(StringComparer.Ordinal)
                           .ToList();

        return matches.Count switch
        {
            0 => (line, matches),
            1 => (matches[0] + " ", matches),
            _ => (CommonPrefix(matches, line), matches)
        };
    }

    // The matches all start with the line, so the prefix is at least as long; the line's own text is kept for it.
    private static string CommonPrefix(List<string> matches, string line)
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

        return line + matches[0][line.Length..length];
    }
}
