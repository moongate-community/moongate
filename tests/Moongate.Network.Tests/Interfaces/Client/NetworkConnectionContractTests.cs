using System.Net;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Interfaces.Client;
using Moongate.Network.Tests.TestSupport.Connections;

namespace Moongate.Network.Tests.Interfaces.Client;

public sealed class NetworkConnectionContractTests
{
    [Fact]
    public void LocalEndpoint_ExistingImplementationWithoutMetadata_ReturnsNull()
    {
        INetworkConnection connection = new LegacyNetworkConnection();
        Assert.Null(connection.LocalEndPoint);
    }

    [Fact]
    public async Task LocalEndpoint_RealTcpConnection_ExposesBoundEndpointThroughInterface()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var accepting = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
        await using var client = await MoongateTcpClient.ConnectAsync((IPEndPoint)listener.LocalEndpoint, cancellationToken: deadline.Token);
        using var peer = await accepting;
        INetworkConnection connection = client;
        Assert.Equal(peer.Client.RemoteEndPoint, connection.LocalEndPoint);
    }
}
