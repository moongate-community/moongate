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
        // Spaces before the name are kept: the command parser skips them.
        var word = line.TrimStart();
        var indent = line[..^word.Length];

        if (word.Any(char.IsWhiteSpace))
        {
            return (line, []);
        }

        var matches = names.Where(name => name.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .Order(StringComparer.Ordinal)
                           .ToList();

        return matches.Count switch
        {
            0 => (line, matches),
            1 => (indent + matches[0] + " ", matches),
            _ => (indent + CommonPrefix(matches), matches)
        };
    }

    // In the case of the names: "SA" with save and saveall gives "save".
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
