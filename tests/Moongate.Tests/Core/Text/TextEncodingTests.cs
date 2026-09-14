using System.Text;
using Moongate.Core.Text;

namespace Moongate.Tests.Core.Text;

public class TextEncodingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetByteLengthForEncoding_Utf32_UsesFourByteCodeUnits(bool bigEndian)
    {
        var encoding = new UTF32Encoding(bigEndian, false);

        Assert.Equal(4, encoding.GetByteLengthForEncoding());
    }

    [Theory]
    [InlineData("hello\u0001", "hello")]
    [InlineData("\u0001", "")]
    public void GetString_TrailingControlCharacter_IsRemovedFromSafeText(string input, string expected)
    {
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(expected, TextEncoding.GetString(bytes, Encoding.UTF8, true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetBytes_MultibyteText_AllocatesExactlyTheEncodedLength(bool useSpan)
    {
        const string input = "Aé😀";

        var bytes = useSpan
                        ? TextEncoding.GetBytes(input.AsSpan(), TextEncoding.UTF8)
                        : TextEncoding.GetBytes(input, TextEncoding.UTF8);

        Assert.Equal(new byte[] { 0x41, 0xC3, 0xA9, 0xF0, 0x9F, 0x98, 0x80 }, bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetBytes_EmptyText_ProducesNoBytes(bool useSpan)
    {
        var bytes = useSpan
                        ? TextEncoding.GetBytes(ReadOnlySpan<char>.Empty, TextEncoding.Unicode)
                        : TextEncoding.GetBytes("", TextEncoding.Unicode);

        Assert.Empty(bytes);
    }

    [Fact]
    public void GetByteLengthForEncoding_AsciiUtf8AndUtf16_ReportsCodeUnitWidths()
    {
        Assert.Equal(1, Encoding.ASCII.GetByteLengthForEncoding());
        Assert.Equal(1, TextEncoding.UTF8.GetByteLengthForEncoding());
        Assert.Equal(2, TextEncoding.Unicode.GetByteLengthForEncoding());
        Assert.Equal(2, TextEncoding.UnicodeLE.GetByteLengthForEncoding());
    }

    [Fact]
    public void UnicodeEncodings_DoNotEmitByteOrderMarks()
    {
        Assert.Empty(TextEncoding.UTF8.GetPreamble());
        Assert.Empty(TextEncoding.Unicode.GetPreamble());
        Assert.Empty(TextEncoding.UnicodeLE.GetPreamble());
    }

    [Fact]
    public void GetBytesAscii_NonAsciiCharacter_UsesQuestionMarkFallback()
    {
        var bytes = "café".AsSpan().GetBytesAscii();

        Assert.Equal(new byte[] { 0x63, 0x61, 0x66, 0x3F }, bytes);
    }

    [Fact]
    public void GetBytesUtf8_UnpairedSurrogate_UsesReplacementCharacter()
    {
        var bytes = "A\uD800B".GetBytesUtf8();

        Assert.Equal(new byte[] { 0x41, 0xEF, 0xBF, 0xBD, 0x42 }, bytes);
    }

    [Fact]
    public void GetBytesBigUni_SurrogatePair_WritesBigEndianCodeUnitsWithinDestination()
    {
        var buffer = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        var count = "A😀".GetBytesBigUni(buffer.AsSpan(1, 6));

        Assert.Equal(6, count);
        Assert.Equal(new byte[] { 0xFF, 0x00, 0x41, 0xD8, 0x3D, 0xDE, 0x00, 0xFF }, buffer);
    }

    [Fact]
    public void GetBytesLittleUni_SurrogatePair_WritesLittleEndianCodeUnitsWithinDestination()
    {
        var buffer = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        var count = "A😀".AsSpan().GetBytesLittleUni(buffer.AsSpan(1, 6));

        Assert.Equal(6, count);
        Assert.Equal(new byte[] { 0xFF, 0x41, 0x00, 0x3D, 0xD8, 0x00, 0xDE, 0xFF }, buffer);
    }

    [Fact]
    public void GetBytesUtf8_DestinationTooSmall_RejectsTruncation()
    {
        var buffer = new byte[1];

        Assert.Throws<ArgumentException>(() => "é".GetBytesUtf8(buffer));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("Caffè 😀", "Caffè 😀")]
    [InlineData("\u001fA \uFFFD\uFFFEB\uFFFF", "A \uFFFDB")]
    [InlineData("\0\tA\r\nB", "AB")]
    public void GetString_SafeText_KeepsPrintableCharactersAndRemovesForbiddenCharacters(
        string input,
        string expected
    )
    {
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(expected, TextEncoding.GetString(bytes, Encoding.UTF8, true));
    }

    [Fact]
    public void GetString_LongSafeTextWithoutFiltering_PreservesTheCompleteString()
    {
        var input = new string('é', 512);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(input, TextEncoding.GetString(bytes, Encoding.UTF8, true));
    }

    [Fact]
    public void GetString_LongTextEndingInControlCharacter_FiltersWithoutDuplicatingThePrefix()
    {
        var expected = new string('a', 512);
        var bytes = Encoding.UTF8.GetBytes(expected + "\u0001");

        Assert.Equal(expected, TextEncoding.GetString(bytes, Encoding.UTF8, true));
    }

    [Fact]
    public void GetString_MalformedUtf8_ReplacesInvalidBytes()
    {
        var bytes = new byte[] { 0x41, 0xFF, 0x42 };

        Assert.Equal("A\uFFFDB", TextEncoding.GetString(bytes, TextEncoding.UTF8));
    }

    [Theory]
    [InlineData(16, false)]
    [InlineData(16, true)]
    [InlineData(512, false)]
    [InlineData(512, true)]
    public void GetString_StackAndPooledPaths_PreserveSafeFiltering(int prefixLength, bool safeString)
    {
        var prefix = new string('a', prefixLength);
        var input = prefix + "\u0001caffè";
        var bytes = Encoding.UTF8.GetBytes(input);

        var result = TextEncoding.GetString(bytes, Encoding.UTF8, safeString);

        Assert.Equal(safeString ? prefix + "caffè" : input, result);
    }
}
