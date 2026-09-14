using System.Buffers;
using Moongate.Core.Extensions.Strings;

namespace Moongate.Tests.Core.Extensions.Strings;

public class StringHelpersTests
{
    [Theory]
    [InlineData("the brave knight", "the Brave Knight")]
    [InlineData("hello world", "Hello World")]
    [InlineData("", "")]
    public void Capitalize_PooledCharacters_PreservesWordRules(string input, string expected)
    {
        Assert.Equal(expected, input.Capitalize());
    }

    [Fact]
    public void Remove_CharacterMatches_PreservesComparisonRules()
    {
        var result = "A-a-a".AsSpan().Remove("a", StringComparison.OrdinalIgnoreCase);

        Assert.Equal("--", result);
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
}
