using Moongate.Network.Compression;
using Moongate.Network.Middlewares;

namespace Moongate.Tests.Network;

public class UoCompressionMiddlewareTests
{
    [Fact]
    public async Task ProcessAsync_Inbound_IsNeverCompressed()
    {
        var middleware = new UoCompressionMiddleware { Enabled = true };
        var data = new byte[] { 0x00, 0x00, 0x00 };

        var result = await middleware.ProcessAsync(null, data);

        Assert.Equal(data, result.ToArray());
    }

    [Fact]
    public async Task ProcessSendAsync_Disabled_PassesThroughUnchanged()
    {
        var middleware = new UoCompressionMiddleware();
        var data = new byte[] { 0x1B, 0x00, 0x0A };

        var result = await middleware.ProcessSendAsync(null, data);

        Assert.Equal(data, result.ToArray());
    }

    [Fact]
    public async Task ProcessSendAsync_Enabled_CompressesToHuffmanBytes()
    {
        var middleware = new UoCompressionMiddleware { Enabled = true };
        var data = new byte[] { 0x00 };

        var result = await middleware.ProcessSendAsync(null, data);

        var expected = new byte[16];
        var written = HuffmanEncoder.Compress(data, expected);

        Assert.Equal(expected.AsSpan(0, written).ToArray(), result.ToArray());
    }

    [Fact]
    public async Task ProcessSendAsync_EnabledButEmpty_ReturnsEmpty()
    {
        var middleware = new UoCompressionMiddleware { Enabled = true };

        var result = await middleware.ProcessSendAsync(null, ReadOnlyMemory<byte>.Empty);

        Assert.True(result.IsEmpty);
    }
}
