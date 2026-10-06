namespace Moongate.Server.Ultima.Services.Books;

/// <summary>
///     Turns the saved text of a document into the pages of the client's book: an empty line of the text is a page
///     break, a page holds eight lines and a longer one goes on in the page after it. Nothing is cut away.
/// </summary>
public static class BookPagination
{
    /// <summary>
    ///     The lines of one page of the client's book.
    /// </summary>
    public const int LinesPerPage = 8;

    /// <summary>
    ///     The most pages a book is sent with.
    /// </summary>
    public const int MaxPages = 255;

    /// <summary>
    ///     The most characters of a line: the client takes fewer than 80, and a player writes that many.
    /// </summary>
    public const int MaxLineLength = 79;

    private const string PageBreak = "\n\n";
    private const string BlankLine = " ";

    /// <summary>
    ///     Splits <paramref name="content" /> into pages; false when it needs more than <see cref="MaxPages" />.
    /// </summary>
    public static bool TryPaginate(string content, out IReadOnlyList<IReadOnlyList<string>> pages)
    {
        ArgumentNullException.ThrowIfNull(content);

        var result = new List<IReadOnlyList<string>>();
        // A body written in a file ends with a line end: it is no line and no page of the book.
        var text = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n');

        foreach (var source in text.Split(PageBreak))
        {
            var lines = source.Length == 0 ? [] : source.Split('\n').SelectMany(Fit).ToArray();

            // A source page with no line is still a page of the book.
            for (var start = 0; start == 0 || start < lines.Length; start += LinesPerPage)
            {
                result.Add(lines.Skip(start).Take(LinesPerPage).ToArray());

                if (result.Count > MaxPages)
                {
                    pages = [];

                    return false;
                }
            }
        }

        pages = result;

        return true;
    }

    /// <summary>
    ///     Writes pages as the saved text <see cref="TryPaginate" /> reads: lines joined by a line end and pages by an
    ///     empty line. An empty line inside a page becomes a line of one space, so the page stays one page; the empty
    ///     lines at the end of a page and the empty pages at the end of the book are not kept.
    /// </summary>
    public static string Join(IReadOnlyList<IReadOnlyList<string>> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var texts = pages.Select(page =>
                {
                    var lines = page.ToList();

                    while (lines.Count > 0 && string.IsNullOrEmpty(lines[^1]))
                    {
                        lines.RemoveAt(lines.Count - 1);
                    }

                    return string.Join('\n', lines.Select(line => line.Length == 0 ? BlankLine : line));
                }
            )
            .ToList();

        while (texts.Count > 0 && texts[^1].Length == 0)
        {
            texts.RemoveAt(texts.Count - 1);
        }

        return string.Join(PageBreak, texts);
    }

    // A line the packet cannot carry is cut at its last space that fits, or inside a word that is longer than a line.
    private static IEnumerable<string> Fit(string line)
    {
        while (line.Length > MaxLineLength)
        {
            var indent = line.Length - line.TrimStart(' ').Length;
            var space = line.LastIndexOf(' ', MaxLineLength);

            if (space > indent)
            {
                yield return line[..space];

                line = line[(space + 1)..];
            }
            else
            {
                yield return line[..MaxLineLength];

                line = line[MaxLineLength..];
            }
        }

        yield return line;
    }
}
