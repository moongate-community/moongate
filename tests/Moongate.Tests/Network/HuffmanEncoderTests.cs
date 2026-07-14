using Moongate.Network.Compression;

namespace Moongate.Tests.Network;

public class HuffmanEncoderTests
{
    [Fact]
    public void CalculateMaxCompressedSize_HugeInput_ReturnsZero()
        => Assert.Equal(0, HuffmanEncoder.CalculateMaxCompressedSize(int.MaxValue));

    [Fact]
    public void CalculateMaxCompressedSize_NonPositiveInput_ReturnsZero()
    {
        Assert.Equal(0, HuffmanEncoder.CalculateMaxCompressedSize(0));
        Assert.Equal(0, HuffmanEncoder.CalculateMaxCompressedSize(-5));
    }

    [Fact]
    public void CalculateMaxCompressedSize_TypicalInput_BoundsWorstCase()

        // worst case 11 bits/byte + 4-bit terminal, rounded up to bytes.
        => Assert.Equal((10 * 11 + 4 + 7) / 8, HuffmanEncoder.CalculateMaxCompressedSize(10));

    [Fact]
    public void Compress_EmptyInput_WritesOnlyTerminalCode()
    {
        // No data bytes: only the 4-bit terminal code, padded up to one byte.
        var output = new byte[16];

        var written = HuffmanEncoder.Compress([], output);

        Assert.Equal(1, written);
        Assert.Equal(0xD0, output[0]);
    }

    [Fact]
    public void Compress_IsDeterministic()
    {
        var input = new byte[] { 0x1B, 0x00, 0x0A, 0xFF, 0x40, 0x7D };
        var first = new byte[64];
        var second = new byte[64];

        var a = HuffmanEncoder.Compress(input, first);
        var b = HuffmanEncoder.Compress(input, second);

        Assert.Equal(a, b);
        Assert.Equal(first.AsSpan(0, a).ToArray(), second.AsSpan(0, b).ToArray());
    }

    [Fact]
    public void Compress_OutputBufferTooSmall_ReturnsZero()
    {
        var input = new byte[64];

        Assert.Equal(0, HuffmanEncoder.Compress(input, new byte[1]));
    }

    [Fact]
    public void Compress_RepetitiveInput_IsSmallerThanInput()
    {
        var input = new byte[256]; // all zeros compress to ~2 bits each
        var output = new byte[HuffmanEncoder.CalculateMaxCompressedSize(input.Length)];

        var written = HuffmanEncoder.Compress(input, output);

        Assert.True(written > 0);
        Assert.True(written < input.Length);
    }

    [Fact]
    public void Compress_SingleZeroByte_ProducesKnownVector()
    {
        // byte 0x00 -> code (2 bits, 0x000), terminal (4 bits, 0x00D), padded to one byte.
        var output = new byte[16];

        var written = HuffmanEncoder.Compress([0x00], output);

        Assert.Equal(1, written);
        Assert.Equal(0x34, output[0]);
    }
}
