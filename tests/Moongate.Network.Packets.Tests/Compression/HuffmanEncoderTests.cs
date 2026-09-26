using Moongate.Network.Packets.Compression;
using Moongate.Network.Packets.Tests.Support;

namespace Moongate.Network.Packets.Tests.Compression;

public class HuffmanEncoderTests
{
    [Fact]
    public void CalculateMaxCompressedSize_HugeInput_ReturnsZero()
    {
        Assert.Equal(0, HuffmanEncoder.CalculateMaxCompressedSize(int.MaxValue));
    }

    [Fact]
    public void CalculateMaxCompressedSize_NonPositiveInput_ReturnsZero()
    {
        Assert.Equal(0, HuffmanEncoder.CalculateMaxCompressedSize(0));
        Assert.Equal(0, HuffmanEncoder.CalculateMaxCompressedSize(-5));
    }

    [Fact]
    public void CalculateMaxCompressedSize_TypicalInput_BoundsWorstCase()
    {
        // Worst case is 11 bits per byte plus the 4-bit terminal code, rounded up to bytes.
        Assert.Equal((10 * 11 + 4 + 7) / 8, HuffmanEncoder.CalculateMaxCompressedSize(10));
    }

    [Fact]
    public void Compress_EmptyInput_WritesOnlyTheTerminalCode()
    {
        // No data: only the 4-bit terminal code, padded up to one byte.
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

        var firstWritten = HuffmanEncoder.Compress(input, first);
        var secondWritten = HuffmanEncoder.Compress(input, second);

        Assert.Equal(firstWritten, secondWritten);
        Assert.Equal(first.AsSpan(0, firstWritten).ToArray(), second.AsSpan(0, secondWritten).ToArray());
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
        var input = new byte[256];
        var output = new byte[HuffmanEncoder.CalculateMaxCompressedSize(input.Length)];

        var written = HuffmanEncoder.Compress(input, output);

        Assert.True(written > 0);
        Assert.True(written < input.Length);
    }

    [Fact]
    public void Compress_SingleZeroByte_ProducesKnownVector()
    {
        // Byte 0x00 is a 2-bit code (00), then the 4-bit terminal (1101), padded to one byte: 0011 0100.
        var output = new byte[16];

        var written = HuffmanEncoder.Compress([0x00], output);

        Assert.Equal(1, written);
        Assert.Equal(0x34, output[0]);
    }

    [Fact]
    public void Compress_SupportedFeaturesPacket_MatchesTheOutputOfModernUo()
    {
        // B9 00FF92D8 is the features packet the game server sends first. The expected bytes come from
        // ModernUO's NetworkCompression, an independent implementation of the same protocol table.
        var output = new byte[16];

        var written = HuffmanEncoder.Compress(Convert.FromHexString("B900FF92D8"), output);

        Assert.Equal("B30C59E409A0", Convert.ToHexString(output.AsSpan(0, written)));
    }

    [Fact]
    public void Compress_ThenTestDecoder_RoundTripsArbitraryBytes()
    {
        var data = Enumerable.Range(0, 512).Select(i => (byte)(i * 31)).ToArray();
        var output = new byte[HuffmanEncoder.CalculateMaxCompressedSize(data.Length)];
        var written = HuffmanEncoder.Compress(data, output);

        Assert.Equal(data, HuffmanDecoder.Decode(output.AsSpan(0, written)));
    }

    [Fact]
    public void Compress_TwoPackets_DecodeBackToBackAsOneStream()
    {
        // Each packet is compressed on its own and ends on a byte boundary, so the client can read them in a row.
        var first = Convert.FromHexString("B900FF92D8");
        var second = Convert.FromHexString("73FF");
        var stream = new List<byte>();

        foreach (var packet in new[] { first, second })
        {
            var output = new byte[HuffmanEncoder.CalculateMaxCompressedSize(packet.Length)];
            stream.AddRange(output.AsSpan(0, HuffmanEncoder.Compress(packet, output)).ToArray());
        }

        Assert.Equal([.. first, .. second], HuffmanDecoder.Decode(stream.ToArray()));
    }
}
