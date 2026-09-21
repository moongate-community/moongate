using Moongate.Core.Extensions.Strings;
using Moongate.Tests.TestSupport.Strings;

namespace Moongate.Tests.Core.Extensions.Strings;

public class InsensitiveStringHelpersTests
{
    [Fact]
    public void InsensitiveAffixes_CharacterCaseDiffers_MatchesOnlyTheCorrectEnd()
    {
        const string source = "Open Door";

        Assert.True(source.InsensitiveStartsWith("open"));
        Assert.False(source.InsensitiveStartsWith("door"));
        Assert.True(source.InsensitiveEndsWith("DOOR"));
        Assert.False(source.AsSpan().InsensitiveEndsWith("OPEN".AsSpan()));
        Assert.True(source.InsensitiveContains('d'));
    }

    [Theory,
     InlineData("alpha", "ALPHA", 0),
     InlineData("alpha", "beta", -1),
     InlineData("gamma", "beta", 1),
     InlineData(null, null, 0),
     InlineData(null, "", -1),
     InlineData("", null, 1)]
    public void InsensitiveCompare_TextAndNulls_OrdersValuesIgnoringCase(string? left, string? right, int expected)
        => Assert.Equal(expected, Math.Sign(left!.InsensitiveCompare(right!)));

    [Fact]
    public void InsensitiveComparisons_TurkishCulture_KeepOrdinalUnicodeRules()
    {
        using var culture = new CultureScope("tr-TR");

        Assert.True("I".InsensitiveEquals("i"));
        Assert.False("I".InsensitiveEquals("ı"));
        Assert.True("é".AsSpan().InsensitiveEquals("É".AsSpan()));
        Assert.False("é".InsensitiveEquals("e\u0301".AsSpan()));
        Assert.True("ä".AsSpan().InsensitiveCompare("z".AsSpan()) > 0);
    }

    [Theory,
     InlineData("Open Door", "open", true),
     InlineData("Open Door", "close", false),
     InlineData("İtem", "item", false)]
    public void InsensitiveContains_Substring_UsesOrdinalCaseFolding(string source, string search, bool expected)
    {
        Assert.Equal(expected, source.InsensitiveContains(search));
        Assert.Equal(expected, source.AsSpan().InsensitiveContains(search.AsSpan()));
    }

    [Fact]
    public void InsensitiveIndexOf_StartOffset_FindsTheNextCommand()
    {
        const string text = "OPEN Door; open gate";

        Assert.Equal(0, text.InsensitiveIndexOf("open"));
        Assert.Equal(11, text.InsensitiveIndexOf("OPEN", 1));
        Assert.Equal(0, text.AsSpan(11).InsensitiveIndexOf("OPEN".AsSpan()));
        Assert.Equal(5, text.InsensitiveIndexOf('d'));
        Assert.Equal(-1, text.InsensitiveIndexOf("close"));
    }

    [Fact]
    public void InsensitiveOperations_NullReceiver_PreserveAbsence()
    {
        string missing = null!;

        Assert.True(missing.InsensitiveEquals(null!));
        Assert.False(missing.InsensitiveEquals(""));
        Assert.False(missing.InsensitiveEquals(ReadOnlySpan<char>.Empty));
        Assert.False(missing.InsensitiveContains("x"));
        Assert.False(missing.InsensitiveContains('x'));
        Assert.False(missing.InsensitiveStartsWith(""));
        Assert.False(missing.InsensitiveEndsWith(""));
        Assert.Equal(-1, missing.InsensitiveIndexOf("x"));
        Assert.Equal(-1, missing.InsensitiveIndexOf('x'));
        Assert.Equal(-1, missing.InsensitiveIndexOf("x", 0));
        Assert.Null(missing.InsensitiveReplace("x", "y"));
    }

    [Fact]
    public void InsensitiveRemove_Destination_WritesOnlyRemainingCharacters()
    {
        var buffer = new[] { '?', '?', '?', '?' };

        "aXXbxxc".AsSpan().InsensitiveRemove("xx".AsSpan(), buffer, out var size);

        Assert.Equal(3, size);
        Assert.Equal("abc?", new(buffer));
    }

    [Fact]
    public void InsensitiveRemove_SubstringMatches_RemovesEachCompleteMatch()
        => Assert.Equal("abc", "aXXbxxc".AsSpan().InsensitiveRemove("xx".AsSpan()));

    [Fact]
    public void InsensitiveReplace_MixedCaseMatches_ReplacesWholeWords()
        => Assert.Equal("gate and gate", "Door and DOOR".InsensitiveReplace("door", "gate"));
}
