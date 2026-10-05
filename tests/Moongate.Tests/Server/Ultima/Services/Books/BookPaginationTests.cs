using Moongate.Server.Ultima.Services.Books;

namespace Moongate.Tests.Server.Ultima.Services.Books;

public sealed class BookPaginationTests
{
    [Fact]
    public void AnEmptyLine_BreaksThePage()
    {
        Assert.True(BookPagination.TryPaginate("one\ntwo\n\nthree", out var pages));

        Assert.Equal([["one", "two"], ["three"]], pages);
    }

    [Fact]
    public void EightLines_AreOnePage_AndTheNinthOpensTheNext()
    {
        Assert.True(BookPagination.TryPaginate(Lines(8), out var eight));
        Assert.True(BookPagination.TryPaginate(Lines(9), out var nine));

        Assert.Single(eight);
        Assert.Equal([8, 1], nine.Select(page => page.Count));
        Assert.Equal("line 9", nine[1][0]);
    }

    // A translated page is longer than its source: what does not fit goes on, and the next source page still
    // opens a page of its own.
    [Fact]
    public void ASourcePageOfThirteenLines_GoesOnInTheNextPage()
    {
        Assert.True(BookPagination.TryPaginate(Lines(13) + "\n\nlast", out var pages));

        Assert.Equal([8, 5, 1], pages.Select(page => page.Count));
        Assert.Equal("line 13", pages[1][4]);
        Assert.Equal(["last"], pages[2]);
    }

    [Fact]
    public void AnEmptySourcePage_IsAnEmptyPage()
    {
        Assert.True(BookPagination.TryPaginate("one\n\n\n\ntwo", out var pages));

        Assert.Equal(3, pages.Count);
        Assert.Empty(pages[1]);
    }

    [Theory]
    [InlineData("one\r\ntwo\r\n\r\nthree")]
    [InlineData("one\rtwo\r\rthree")]
    public void OtherLineEnds_AreNewLines(string content)
    {
        Assert.True(BookPagination.TryPaginate(content, out var pages));

        Assert.Equal([["one", "two"], ["three"]], pages);
    }

    // A body written in a TOML file ends with a line end: it is no line and no page of the book.
    [Theory]
    [InlineData("one\ntwo\n")]
    [InlineData("one\ntwo\n\n")]
    [InlineData("one\ntwo\r\n\r\n\r\n")]
    public void LineEndsAtTheEnd_AddNothing(string content)
    {
        Assert.True(BookPagination.TryPaginate(content, out var pages));

        Assert.Equal([["one", "two"]], pages);
    }

    // A line of one space is a blank line of the page, not a page break.
    [Fact]
    public void ALineOfOneSpace_IsABlankLineOfThePage()
    {
        Assert.True(BookPagination.TryPaginate("one\n \ntwo", out var pages));

        Assert.Equal([["one", " ", "two"]], pages);
    }

    [Fact]
    public void TheIndentOfALine_IsKept()
    {
        Assert.True(BookPagination.TryPaginate("    In a time before\ntime", out var pages));

        Assert.Equal("    In a time before", pages[0][0]);
    }

    [Fact]
    public void ALineTooLong_IsCutAtSpaces()
    {
        var line = string.Join(' ', Enumerable.Repeat("word", 40));

        Assert.True(BookPagination.TryPaginate(line, out var pages));

        var lines = pages.SelectMany(page => page).ToArray();
        Assert.True(lines.Length > 1);
        Assert.All(lines, cut => Assert.InRange(cut.Length, 1, BookPagination.MaxLineLength));
        Assert.Equal(line, string.Join(' ', lines));
    }

    [Fact]
    public void AWordTooLong_IsCutHard_AndNothingIsLost()
    {
        var word = new string('x', 200);

        Assert.True(BookPagination.TryPaginate(word, out var pages));

        var lines = pages.SelectMany(page => page).ToArray();
        Assert.All(lines, cut => Assert.InRange(cut.Length, 1, BookPagination.MaxLineLength));
        Assert.Equal(word, string.Concat(lines));
    }

    [Fact]
    public void MorePagesThanABookHolds_IsRefused()
    {
        var content = string.Join("\n\n", Enumerable.Range(1, BookPagination.MaxPages + 1).Select(page => $"page {page}"));
        var fits = string.Join("\n\n", Enumerable.Range(1, BookPagination.MaxPages).Select(page => $"page {page}"));

        Assert.False(BookPagination.TryPaginate(content, out _));
        Assert.True(BookPagination.TryPaginate(fits, out var pages));
        Assert.Equal(BookPagination.MaxPages, pages.Count);
    }

    private static string Lines(int count)
    {
        return string.Join('\n', Enumerable.Range(1, count).Select(line => $"line {line}"));
    }
}
