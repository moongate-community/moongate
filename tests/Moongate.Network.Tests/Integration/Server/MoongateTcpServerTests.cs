using System.Net;
using System.Net.Sockets;

using Moongate.Network.Server;
using Moongate.Network.Tests.Support;

namespace Moongate.Network.Tests.Integration.Server;

public sealed class MoongateTcpServerTests
{
    [Fact]
    public async Task StartAsync_LoopbackClient_DeliversACompleteFrame()
    {
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new MoongateTcpServer(
            new IPEndPoint(IPAddress.Loopback, 0), framer: new LengthPrefixFramer());
        server.OnDataReceived += (_, args) => received.TrySetResult(args.Data.ToArray());
        await server.StartAsync(CancellationToken.None);
        Assert.True(server.IsRunning);
        Assert.InRange(server.Port, 1, 65535);

        using var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, server.Port);
        await peer.GetStream().WriteAsync(new byte[] { 1, 0x42 });

        Assert.Equal(new byte[] { 1, 0x42 }, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
