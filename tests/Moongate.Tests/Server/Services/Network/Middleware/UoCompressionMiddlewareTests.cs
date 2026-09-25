using Moongate.Server.Services.Network.Middleware;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Support.Sessions;

namespace Moongate.Tests.Server.Services.Network.Middleware;

public sealed class UoCompressionMiddlewareTests
{
    // The features packet the game server sends first, and its compressed form as ModernUO produces it.
    private static readonly byte[] Packet = Convert.FromHexString("B900FF92D8");
    private static readonly byte[] CompressedPacket = Convert.FromHexString("B30C59E409A0");

    [Fact]
    public async Task ProcessAsync_CompressionOn_LeavesReceivedBytesUntouched()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        sessions.GetOrCreate(fixture.Client).NetworkSession.EnableCompression();
        var middleware = new UoCompressionMiddleware(sessions);

        var result = await middleware.ProcessAsync(fixture.Client, Packet);

        Assert.Equal(Packet, result.ToArray());
    }

    [Fact]
    public async Task ProcessSendAsync_CompressionOff_PassesTheBytesThrough()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        sessions.GetOrCreate(fixture.Client);
        var middleware = new UoCompressionMiddleware(sessions);

        var result = await middleware.ProcessSendAsync(fixture.Client, Packet);

        Assert.Equal(Packet, result.ToArray());
    }

    [Fact]
    public async Task ProcessSendAsync_CompressionOn_CompressesThePayload()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        sessions.GetOrCreate(fixture.Client).NetworkSession.EnableCompression();
        var middleware = new UoCompressionMiddleware(sessions);

        var result = await middleware.ProcessSendAsync(fixture.Client, Packet);

        Assert.Equal(CompressedPacket, result.ToArray());
    }

    [Fact]
    public async Task ProcessSendAsync_CompressionOnForOneSession_DoesNotAffectAnother()
    {
        await using var compressed = await SessionFixture.CreateAsync();
        await using var plain = await SessionFixture.CreateAsync();
        var sessions = new SessionService(compressed.Loop);
        sessions.GetOrCreate(compressed.Client).NetworkSession.EnableCompression();
        sessions.GetOrCreate(plain.Client);
        var middleware = new UoCompressionMiddleware(sessions);

        var compressedResult = await middleware.ProcessSendAsync(compressed.Client, Packet);
        var plainResult = await middleware.ProcessSendAsync(plain.Client, Packet);

        Assert.Equal(CompressedPacket, compressedResult.ToArray());
        Assert.Equal(Packet, plainResult.ToArray());
    }

    [Fact]
    public async Task ProcessSendAsync_EmptyPayloadOrNoClientOrNoSession_PassesTheBytesThrough()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var middleware = new UoCompressionMiddleware(sessions);

        var empty = await middleware.ProcessSendAsync(fixture.Client, ReadOnlyMemory<byte>.Empty);
        var noClient = await middleware.ProcessSendAsync(null, Packet);
        var noSession = await middleware.ProcessSendAsync(fixture.Client, Packet);

        Assert.True(empty.IsEmpty);
        Assert.Equal(Packet, noClient.ToArray());
        Assert.Equal(Packet, noSession.ToArray());
    }

    [Fact]
    public async Task ProcessSendAsync_PayloadTooLargeToCompress_Throws()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        sessions.GetOrCreate(fixture.Client).NetworkSession.EnableCompression();
        var middleware = new UoCompressionMiddleware(sessions);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await middleware.ProcessSendAsync(fixture.Client, new byte[300_000])
        );
    }
}
