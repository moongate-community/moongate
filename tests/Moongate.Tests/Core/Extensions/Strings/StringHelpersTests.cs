using System.Buffers;
using Moongate.Core.Buffers;
using Moongate.Core.Extensions.Strings;
using Moongate.Tests.TestSupport.Strings;

namespace Moongate.Tests.Core.Extensions.Strings;

public class StringHelpersTests
{
    [Theory, InlineData("apple", true, "an apple of valor"), InlineData("sword", false, "a sword of valor")]
    public void AppendSpaceWithArticle_MultipleWords_AddsArticleOnlyAtTheBeginning(
        string noun,
        bool articleAn,
        string expected
    )
    {
        var builder = new ValueStringBuilder(stackalloc char[64]);

        try
        {
            builder.AppendSpaceWithArticle(noun, articleAn);
            builder.AppendSpaceWithArticle("of valor", true);

            Assert.Equal(expected, builder.ToString());
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Theory,
     InlineData("the brave knight", "the Brave Knight"),
     InlineData("hello world", "Hello World"),
     InlineData("hello ", "Hello "),
     InlineData("hello  world", "Hello  World"),
     InlineData("the ", "the "),
     InlineData("", ""),
     InlineData(null, null)]
    public void Capitalize_PooledCharacters_PreservesWordRules(string? input, string? expected)
        => Assert.Equal(expected, input!.Capitalize());

    [Fact]
    public void Capitalize_TurkishCurrentCulture_UsesInvariantInitials()
    {
        using var culture = new CultureScope("tr-TR");

        Assert.Equal("Istanbul Izmir", "istanbul izmir".Capitalize());
    }

    [Theory,
     InlineData(null, "fallback"),
     InlineData("", "fallback"),
     InlineData(" \t\r\n", "fallback"),
     InlineData("  value  ", "  value  ")]
    public void DefaultIfNullOrEmpty_BlankOrPopulatedText_SelectsTheAppropriateValue(
        string? input,
        string expected
    )
        => Assert.Equal(expected, input!.DefaultIfNullOrEmpty("fallback"));

    [Fact]
    public void IndentMultiline_CustomSeparator_IndentsEmptyAndTrailingLines()
    {
        var result = "one\r\n\r\ntwo\r\n".IndentMultiline("> ", "\r\n");

        Assert.Equal("> one\r\n> \r\n> two\r\n> ", result);
    }

    [Theory,
     InlineData(new byte[] { 0x41, 0x42, 0x00, 0x43 }, 1, 2),
     InlineData(new byte[] { 0x41, 0x00, 0x00, 0x42, 0x00, 0x00 }, 2, 4),
     InlineData(new byte[] { 0x41, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 4, 4),
     InlineData(new byte[] { 0x41, 0x00 }, 2, -1),
     InlineData(new byte[] { 0x41, 0x00, 0x00 }, 2, -1)]
    public void IndexOfTerminator_EncodedCodeUnits_ReturnsAlignedByteOffset(byte[] bytes, int width, int expected)
    {
        Assert.Equal(expected, bytes.AsSpan().IndexOfTerminator(width));
        Assert.Equal(expected, ((ReadOnlySpan<byte>)bytes).IndexOfTerminator(width));
    }

    [Fact]
    public void Remove_CharacterMatches_PreservesComparisonRules()
    {
        var result = "A-a-a".AsSpan().Remove("a", StringComparison.OrdinalIgnoreCase);

        Assert.Equal("--", result);
    }

    [Theory, InlineData(StringComparison.InvariantCulture), InlineData(StringComparison.CurrentCulture)]
    public void Remove_CulturalMatchWithInsufficientBuffer_ThrowsWithoutWriting(StringComparison comparison)
    {
        using var culture = new CultureScope("fr-FR");
        var buffer = new[] { '?' };

        Assert.Throws<OutOfMemoryException>(() => "xe\u0301y".AsSpan().Remove("é", comparison, buffer, out _));

        Assert.Equal('?', buffer[0]);
    }

    [Theory,
     InlineData("é", "e\u0301", "", StringComparison.InvariantCulture),
     InlineData("e\u0301", "é", "", StringComparison.InvariantCulture),
     InlineData("xéy", "e\u0301", "xy", StringComparison.CurrentCulture),
     InlineData("xe\u0301y", "é", "xy", StringComparison.CurrentCulture)]
    public void Remove_CulturallyEquivalentTextIntoBuffer_PreservesCharactersOutsideTheResult(
        string source,
        string search,
        string expected,
        StringComparison comparison
    )
    {
        using var culture = new CultureScope("fr-FR");
        var buffer = "????".ToCharArray();

        source.AsSpan().Remove(search, comparison, buffer.AsSpan(1, expected.Length), out var size);

        Assert.Equal(expected.Length, size);
        Assert.Equal(expected == "" ? "????" : "?xy?", new(buffer));
    }

    [Theory,
     InlineData("é", "e\u0301", "", StringComparison.InvariantCulture),
     InlineData("e\u0301", "é", "", StringComparison.InvariantCulture),
     InlineData("xéy", "e\u0301", "xy", StringComparison.CurrentCulture),
     InlineData("xe\u0301y", "é", "xy", StringComparison.CurrentCulture)]
    public void Remove_CulturallyEquivalentText_RemovesTheMatchedSourceCharacters(
        string source,
        string search,
        string expected,
        StringComparison comparison
    )
    {
        using var culture = new CultureScope("fr-FR");

        Assert.Equal(expected, source.AsSpan().Remove(search, comparison));
    }

    [Fact]
    public void Remove_EmptyMatchWithDestination_RejectsTheInvalidSearchTerm()
    {
        var buffer = new char[3];

        Assert.Throws<ArgumentException>(() => "abc".AsSpan().Remove("", StringComparison.Ordinal, buffer, out _));
    }

    [Fact]
    public void Remove_EmptyMatch_RejectsTheInvalidSearchTerm()
        => Assert.Throws<ArgumentException>(() => "abc".AsSpan().Remove("", StringComparison.Ordinal));

    [Fact]
    public void Remove_EmptySource_WritesNothingToTheDestination()
    {
        var buffer = new[] { '?' };

        ReadOnlySpan<char>.Empty.Remove("x", StringComparison.Ordinal, buffer, out var size);

        Assert.Equal(0, size);
        Assert.Equal('?', buffer[0]);
    }

    [Fact]
    public void Remove_ExactSizeBuffer_WritesOnlyTheRemainingCharacters()
    {
        var buffer = new[] { '?', '?', '?', '?', '?' };

        "a--b--c".AsSpan().Remove("--", StringComparison.Ordinal, buffer.AsSpan(1, 3), out var size);

        Assert.Equal(3, size);
        Assert.Equal("?abc?", new(buffer));
    }

    [Fact]
    public void Remove_InsufficientBuffer_ReportsFailure()
    {
        var buffer = new char[2];

        Assert.Throws<OutOfMemoryException>(() => "a--b--c".AsSpan().Remove("--", StringComparison.Ordinal, buffer, out _));
    }

    [Fact]
    public void Remove_InvalidComparison_Throws()
        => Assert.Throws<ArgumentException>(() => "abc".AsSpan().Remove("b", (StringComparison)99));

    [Fact]
    public void Remove_MultipleCharacterMatches_RemovesTheWholeMatch()
    {
        var result = "a--b--c".AsSpan().Remove("--", StringComparison.Ordinal);

        Assert.Equal("abc", result);
    }

    [Fact]
    public void ReplaceAny_MatchingCharacters_UsesTheirCorrespondingReplacements()
    {
        var characters = "a/b:c/d:e".ToCharArray();

        characters.AsSpan().ReplaceAny("/:".AsSpan(), "-_".AsSpan());

        Assert.Equal("a-b_c-d_e", new(characters));
    }

    [Fact]
    public void ToPooledArray_CopiesText_ProvidesCallerOwnedBuffer()
    {
        var array = "hello".ToPooledArray();

        try
        {
            Assert.Equal("hello", array.AsSpan(0, 5).ToString());
        }
        finally
        {
            ArrayPool<char>.Shared.Return(array);
        }
    }

    [Fact]
    public void TrimMultiline_CustomSeparator_TrimsEachLineAndPreservesTrailingLine()
    {
        var result = " one \r\n\t two \r\n".TrimMultiline("\r\n");

        Assert.Equal("one\r\ntwo\r\n", result);
    }

    [Theory, InlineData(null), InlineData(""), InlineData(" \t\r\n")]
    public void Wrap_BlankText_ProducesNoLines(string? input)
        => Assert.Null(input!.Wrap(10, 2));

    [Fact]
    public void Wrap_LongWordAfterExistingLine_RespectsTheTotalLineLimit()
        => Assert.Equal(new[] { "one", "abcd" }, "one abcdefghijk".Wrap(4, 2));

    [Fact]
    public void Wrap_LongWordReachesLineLimit_StopsBeforeRemainingCharacters()
        => Assert.Equal(new[] { "abcd" }, "abcdefghijk".Wrap(4, 1));

    [Fact]
    public void Wrap_LongWord_SplitsAtTheRequestedWidth()
        => Assert.Equal(new[] { "abcd", "efgh", "ijk" }, "abcdefghijk".Wrap(4, 10));

    [Fact]
    public void Wrap_WordWouldExceedLineWidth_MovesItToTheNextLine()
        => Assert.Equal(new[] { "one two", "three", "four" }, "  one two three four  ".Wrap(7, 10));

    [Fact]
    public void Wrap_WordsReachLineLimit_StopsBeforeRemainingWords()
        => Assert.Equal(new[] { "one", "two" }, "one two three".Wrap(4, 2));
}
