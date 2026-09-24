namespace Moongate.UoxItemConverter.Internal;

/// <summary>
/// Reads UOX3's <c>.dfn</c> block format: <c>// comment</c> lines, blank lines, a
/// <c>[header]</c> line, a bare <c>{</c>, one line per entry, and a bare <c>}</c>. A trailing
/// <c>//comment</c> is stripped from every line first, real data has it glued straight onto a brace
/// with no space (<c>{//approximately 1%</c>), which otherwise hides the whole block: the real
/// engine (<c>oldstrutil::removeTrailing(sLine, "//")</c> in UOX3's own <c>ssection.cpp</c>) does the
/// same, unconditionally, before looking at a line's content.
/// </summary>
internal static class DfnParser
{
    public static List<DfnBlock> Parse(IReadOnlyList<string> lines)
    {
        var blocks = new List<DfnBlock>();
        string? header = null;
        Dictionary<string, string>? fields = null;
        List<string>? entries = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            var commentIndex = line.IndexOf("//", StringComparison.Ordinal);

            if (commentIndex >= 0)
            {
                line = line[..commentIndex].TrimEnd();
            }

            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                header = line[1..^1];
                fields = null;
                entries = null;

                continue;
            }

            if (line == "{")
            {
                fields = new(StringComparer.OrdinalIgnoreCase);
                entries = [];

                continue;
            }

            if (line == "}")
            {
                if (header is not null && fields is not null && entries is not null)
                {
                    blocks.Add(new(header, fields, entries));
                }

                header = null;
                fields = null;
                entries = null;

                continue;
            }

            if (fields is null || entries is null)
            {
                continue;
            }

            entries.Add(line);

            var separator = line.IndexOf('=');

            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            fields[key] = value;
        }

        return blocks;
    }
}
