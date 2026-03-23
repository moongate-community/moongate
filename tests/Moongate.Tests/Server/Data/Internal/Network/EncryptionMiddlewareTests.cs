using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Internal.Network;
using Moongate.Server.Data.Session;
using Moongate.Tests.Network.Support;

namespace Moongate.Tests.Server.Data.Internal.Network;

public sealed class EncryptionMiddlewareTests
{
    [Test]
    public async Task ProcessAsync_WhenSessionHasEncryption_ShouldDecryptInboundPayload()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameNetworkSession(client);
        var encryption = new TestClientEncryption();
        session.EnableEncryption(encryption);
        var middleware = new EncryptionMiddleware(session);
        var payload = new byte[] { 0xAA, 0xAB };

        var result = await middleware.ProcessAsync(client, payload, CancellationToken.None);

        Assert.That(result.ToArray(), Is.EqualTo(new byte[] { 0x00, 0x01 }));
    }

    [Test]
    public async Task ProcessAsync_WhenSessionHasNoEncryption_ShouldPassThrough()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameNetworkSession(client);
        var middleware = new EncryptionMiddleware(session);
        var payload = new byte[] { 0x01, 0x02 };

        var result = await middleware.ProcessAsync(client, payload, CancellationToken.None);

        Assert.That(result.ToArray(), Is.EqualTo(payload));
    }

    [Test]
    public async Task ProcessSendAsync_WhenSessionHasEncryption_ShouldEncryptOutboundPayload()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameNetworkSession(client);
        var encryption = new TestClientEncryption();
        session.EnableEncryption(encryption);
        var middleware = new EncryptionMiddleware(session);
        var payload = new byte[] { 0x55, 0x54 };

        var result = await middleware.ProcessSendAsync(client, payload, CancellationToken.None);

        Assert.That(result.ToArray(), Is.EqualTo(new byte[] { 0x00, 0x01 }));
    }
}
