using Moongate.UoxItemConverter.Internal;

namespace Moongate.UoxItemConverter.Tests.Internal;

public sealed class UoxNumberTests
{
    [Theory,
     InlineData("12", 12),
     InlineData("-3", -3),
     InlineData("0x0F", 15),
     InlineData(" 0x0c4f 0x0c50 ", 0x0C4F),
     InlineData("0x0x04FC", 0x04FC),
     InlineData("0x15b6]", 0x15B6)]
    public void TryParse_ReadsHexOrDecimal_ForgivingUox3Typos(string text, int expected)
    {
        Assert.True(UoxNumber.TryParse(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory, InlineData(""), InlineData("abc"), InlineData("0xZZ")]
    public void TryParse_NotANumber_IsFalse(string text)
    {
        Assert.False(UoxNumber.TryParse(text, out _));
    }
}
