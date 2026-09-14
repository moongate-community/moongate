using Moongate.Core.Extensions.Strings;
using Moongate.Tests.TestSupport.Strings;

namespace Moongate.Tests.Core.Extensions.Strings;

public class OrdinalStringHelpersTests
{
    [Theory]
    [InlineData("alpha", "alpha", 0)]
    [InlineData("Alpha", "alpha", -1)]
    [InlineData("gamma", "beta", 1)]
    [InlineData(null, null, 0)]
    [InlineData(null, "", -1)]
    [InlineData("", null, 1)]
    public void CompareOrdinal_TextAndNulls_OrdersValuesByCodeUnit(string? left, string? right, int expected)
    {
        Assert.Equal(expected, Math.Sign(left!.CompareOrdinal(right!)));
    }

    [Fact]
    public void OrdinalComparisons_GermanCulture_DoNotUseLinguisticSortingOrNormalization()
    {
        using var culture = new CultureScope("de-DE");

        Assert.True("ä".AsSpan().CompareOrdinal("z".AsSpan()) > 0);
        Assert.False("é".AsSpan().EqualsOrdinal("e\u0301"));
        Assert.False("I".EqualsOrdinal("i"));
        Assert.True("é".EqualsOrdinal("é"));
    }

    [Fact]
    public void IndexOfOrdinal_StartOffset_FindsOnlyMatchingCase()
    {
        const string text = "door Door door";

        Assert.Equal(0, text.IndexOfOrdinal("door"));
        Assert.Equal(10, text.IndexOfOrdinal("door", 1));
        Assert.Equal(5, text.IndexOfOrdinal('D'));
        Assert.Equal(-1, text.AsSpan().IndexOfOrdinal("DOOR".AsSpan()));
    }

    [Theory]
    [InlineData("Door", true)]
    [InlineData("door", false)]
    [InlineData("", true)]
    public void ContainsOrdinal_Substring_RequiresExactCase(string search, bool expected)
    {
        Assert.Equal(expected, "Open Door".ContainsOrdinal(search));
        Assert.Equal(expected, "Open Door".AsSpan().ContainsOrdinal(search.AsSpan()));
    }

    [Fact]
    public void OrdinalAffixes_CaseDiffers_RequireAnExactMatch()
    {
        const string source = "Open Door";

        Assert.True(source.StartsWithOrdinal("Open"));
        Assert.False(source.StartsWithOrdinal("open"));
        Assert.True(source.EndsWithOrdinal("Door"));
        Assert.False(source.AsSpan().EndsWithOrdinal("door".AsSpan()));
        Assert.True(source.ContainsOrdinal('D'));
        Assert.False(source.ContainsOrdinal('d'));
    }

    [Fact]
    public void OrdinalCharacterSearch_OnASlice_UsesOnlyTheRequestedCharacters()
    {
        var source = "[Door]".AsSpan(1, 4);

        Assert.True(source.StartsWithOrdinal('D'));
        Assert.True(source.EndsWithOrdinal('r'));
        Assert.False(source.StartsWithOrdinal('['));
        Assert.False(source.EndsWithOrdinal(']'));
        Assert.Equal(1, source.IndexOfOrdinal('o'));
        Assert.Equal(-1, source.IndexOfOrdinal(']'));
    }

    [Fact]
    public void ReplaceOrdinal_MixedCaseMatches_ReplacesOnlyExactMatches()
    {
        Assert.Equal("gate DOOR gate", "door DOOR door".ReplaceOrdinal("door", "gate"));
    }

    [Fact]
    public void RemoveOrdinal_MultipleCharacterMatches_PreservesOtherCasing()
    {
        Assert.Equal("aXXbc", "aXXbxxc".RemoveOrdinal("xx"));
        Assert.Equal("aXXbc", "aXXbxxc".AsSpan().RemoveOrdinal("xx".AsSpan()));
    }

    [Fact]
    public void RemoveOrdinal_Destination_WritesOnlyTheRemainingCharacters()
    {
        var buffer = new[] { '?', '?', '?', '?' };

        "a--b--c".AsSpan().RemoveOrdinal("--".AsSpan(), buffer, out var size);

        Assert.Equal(3, size);
        Assert.Equal("abc?", new string(buffer));
    }

    [Fact]
    public void OrdinalOperations_NullReceiver_PreserveAbsence()
    {
        string missing = null!;

        Assert.True(missing.EqualsOrdinal(null!));
        Assert.False(missing.EqualsOrdinal(""));
        Assert.False(missing.ContainsOrdinal("x"));
        Assert.False(missing.ContainsOrdinal('x'));
        Assert.False(missing.StartsWithOrdinal(""));
        Assert.False(missing.EndsWithOrdinal(""));
        Assert.Equal(-1, missing.IndexOfOrdinal("x"));
        Assert.Equal(-1, missing.IndexOfOrdinal('x'));
        Assert.Equal(-1, missing.IndexOfOrdinal("x", 0));
        Assert.Null(missing.RemoveOrdinal("x"));
        Assert.Null(missing.ReplaceOrdinal("x", "y"));
    }
}
