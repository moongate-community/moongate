using System.IO.Compression;
using Moongate.Ultima.Helpers;

namespace Moongate.Tests.Ultima.Helpers;

public class UopUtilsTests
{
    private static readonly byte[] _expectedData = "Moongate zlib compatibility."u8.ToArray();

    // Independent fixtures generated with Python's zlib.compress at levels 0, 6 and 9.
    private const string StoredZlibPayload = "7801011C00E3FF4D6F6F6E67617465207A6C696220636F6D7061746962696C6974792E9C7F0AD4";
    private const string ZlibPayload = "789CF3CDCFCF4B4F2C4955A8CAC94C5248CECF2D482CC94CCACCC92CA9D403009C7F0AD4";
    private const string BestZlibPayload = "78DAF3CDCFCF4B4F2C4955A8CAC94C5248CECF2D482CC94CCACCC92CA9D403009C7F0AD4";

    [Theory, InlineData(null), InlineData(new byte[0])]
    public void Compress_NullOrEmptyInput_ReturnsFailure(byte[]? input)
    {
        var (success, data) = UopUtils.Compress(input!);

        Assert.False(success);
        Assert.Empty(data);
    }

    [Theory, InlineData(null), InlineData(new byte[0])]
    public void Decompress_NullOrEmptyInput_ReturnsFailure(byte[]? input)
    {
        var (success, data) = UopUtils.Decompress(input!);

        Assert.False(success);
        Assert.Empty(data);
    }

    [Fact]
    public void Compress_KnownPayload_ProducesCompleteZlibDataWithoutChangingInput()
    {
        var input = _expectedData.ToArray();

        var (success, compressed) = UopUtils.Compress(input);

        Assert.True(success);
        Assert.Equal(_expectedData, input);
        // The checksum is independent of the chosen DEFLATE representation.
        Assert.Equal(Convert.FromHexString("9C7F0AD4"), compressed[^4..]);

        using var source = new MemoryStream(compressed);
        using var zlib = new ZLibStream(source, CompressionMode.Decompress);
        using var restored = new MemoryStream();
        zlib.CopyTo(restored);

        Assert.Equal(_expectedData, restored.ToArray());
    }

    [Theory, InlineData(1, false), InlineData(131072, false), InlineData(131072, true)]
    public void Compression_RoundTrip_PreservesBinaryData(int length, bool repetitive)
    {
        var input = new byte[length];

        if (repetitive)
        {
            Array.Fill(input, (byte)0xA5);
        }
        else
        {
            new Random(12345).NextBytes(input);
        }

        var (compressedSuccessfully, compressed) = UopUtils.Compress(input);
        Assert.True(compressedSuccessfully);

        var (decompressedSuccessfully, restored) = UopUtils.Decompress(compressed);
        Assert.True(decompressedSuccessfully);
        Assert.Equal(input, restored);

        var destination = new byte[input.Length];
        Assert.True(UopUtils.TryDecompressInto(compressed, 0, compressed.Length, destination, out var written));
        Assert.Equal(input.Length, written);
        Assert.Equal(input, destination);
    }

    [Theory, InlineData(StoredZlibPayload), InlineData(ZlibPayload), InlineData(BestZlibPayload)]
    public void Decompress_IndependentZlibPayload_ReturnsOriginalData(string hex)
    {
        var compressed = Convert.FromHexString(hex);

        var (success, data) = UopUtils.Decompress(compressed);

        Assert.True(success);
        Assert.Equal(_expectedData, data);
    }

    [Theory,
     InlineData("00000000"),
     // Correct zlib header and data, but an invalid Adler-32 checksum.
     InlineData("789CF3CDCFCF4B4F2C4955A8CAC94C5248CECF2D482CC94CCACCC92CA9D403009C7F0AD5"),
     // The same payload in raw DEFLATE and gzip formats must not be accepted as zlib.
     InlineData("F3CDCFCF4B4F2C4955A8CAC94C5248CECF2D482CC94CCACCC92CA9D40300"),
     InlineData("1F8B0800000000000003F3CDCFCF4B4F2C4955A8CAC94C5248CECF2D482CC94CCACCC92CA9D403007CB08B1A1C000000")]
    public void Decompression_InvalidZlibPayload_ReturnsFailure(string hex)
    {
        var compressed = Convert.FromHexString(hex);

        var (success, data) = UopUtils.Decompress(compressed);

        Assert.False(success);
        Assert.Empty(data);
        Assert.False(UopUtils.TryDecompressInto(compressed, 0, compressed.Length, new byte[64], out var written));
        Assert.Equal(0, written);
    }

    [Theory, InlineData(28), InlineData(64)]
    public void TryDecompressInto_InputSlice_WritesOnlyDecompressedBytes(int capacity)
    {
        var compressed = Convert.FromHexString(ZlibPayload);
        var source = new byte[compressed.Length + 8];
        Array.Fill(source, (byte)0xFF);
        compressed.CopyTo(source, 3);
        var destination = new byte[capacity];
        Array.Fill(destination, (byte)0xCC);

        var success = UopUtils.TryDecompressInto(source, 3, compressed.Length, destination, out var written);

        Assert.True(success);
        Assert.Equal(_expectedData.Length, written);
        Assert.Equal(_expectedData, destination[..written]);
        Assert.All(destination[written..], value => Assert.Equal((byte)0xCC, value));
    }

    [Theory, InlineData(0), InlineData(27)]
    public void TryDecompressInto_DestinationTooSmall_ReturnsFailureAndZeroLength(int capacity)
    {
        var compressed = Convert.FromHexString(ZlibPayload);

        var success = UopUtils.TryDecompressInto(compressed, 0, compressed.Length, new byte[capacity], out var written);

        Assert.False(success);
        Assert.Equal(0, written);
    }

    [Theory,
     InlineData(-1, 1),
     InlineData(0, -1),
     InlineData(0, 0),
     InlineData(int.MaxValue, 1),
     InlineData(1, int.MaxValue)]
    public void TryDecompressInto_InvalidInputSlice_ReturnsFailureAndZeroLength(int offset, int length)
    {
        var compressed = Convert.FromHexString(ZlibPayload);

        var success = UopUtils.TryDecompressInto(compressed, offset, length, new byte[64], out var written);

        Assert.False(success);
        Assert.Equal(0, written);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void TryDecompressInto_NullBuffer_ReturnsFailureAndZeroLength(bool nullSource)
    {
        var compressed = Convert.FromHexString(ZlibPayload);

        var success = UopUtils.TryDecompressInto(
            nullSource ? null! : compressed,
            0,
            compressed.Length,
            nullSource ? new byte[64] : null!,
            out var written
        );

        Assert.False(success);
        Assert.Equal(0, written);
    }

    [Fact]
    public void Decompression_ValidEmptyZlibPayload_SucceedsWithEmptyOutput()
    {
        var compressed = Convert.FromHexString("789C030000000001");

        var (success, data) = UopUtils.Decompress(compressed);

        Assert.True(success);
        Assert.Empty(data);
        Assert.True(UopUtils.TryDecompressInto(compressed, 0, compressed.Length, Array.Empty<byte>(), out var written));
        Assert.Equal(0, written);
    }
}
