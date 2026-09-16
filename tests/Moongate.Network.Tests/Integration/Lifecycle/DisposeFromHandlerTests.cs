using System.Net;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Server;

namespace Moongate.Network.Tests.Integration.Lifecycle;

/// <summary>
/// Handlers run on a connection's receive loop, so kicking a client from inside <c>OnDataReceived</c>
/// disposes the very object whose loop is executing. The synchronous dispose path must therefore
/// never wait on that loop. Every wait here is bounded so a regression fails fast instead of hanging
/// the suite.
/// </summary>
public sealed class DisposeFromHandlerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ClientDispose_CalledFromDataReceivedHandler_Completes()
    {
        // Arrange
        var (sender, receiver) = await ConnectedPairAsync();
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        receiver.OnDataReceived += (_, e) =>
                                   {
                                       e.Client.Dispose();
                                       disposed.TrySetResult();
                                   };

        try
        {
            // Act
            await sender.SendAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);

            // Assert
            await disposed.Task.WaitAsync(Timeout);
            Assert.False(receiver.IsConnected);
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }

    // Weaker than its client-side twin, and kept as a structural guard rather than as a regression
    // detector. Against the pre-fix server the deadlock this targets was real, but the race that
    // exposes it was reliably lost, so this test passed against the broken code too. Turning it into
    // a real detector means strengthening the server's DisposeAsync, which is out of scope here.
    [Fact]
    public async Task ServerDispose_CalledFromDataReceivedHandler_Completes()
    {
        // Arrange
        // The server's synchronous dispose must not drain its clients: it runs on the receive loop of
        // the very client a drain would wait for. It closes the listener and every client socket
        // instead, and leaves both loops to unwind on their own.
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));

        server.OnDataReceived += (_, _) =>
                                 {
                                     server.Dispose();
                                     disposed.TrySetResult();
                                 };
        await server.StartAsync(CancellationToken.None);

        var client = await MoongateTcpClient.ConnectAsync(new(IPAddress.Loopback, server.Port));

        try
        {
            // Act
            await client.SendAsync(new byte[] { 4, 5, 6 }, CancellationToken.None);

            // Assert
            await disposed.Task.WaitAsync(Timeout);
            Assert.False(server.IsRunning);
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    private static async Task<(MoongateTcpClient Sender, MoongateTcpClient Receiver)> ConnectedPairAsync()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        var port = ((IPEndPoint)listener.LocalEndPoint!).Port;

        var senderSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var connectTask = senderSocket.ConnectAsync(IPAddress.Loopback, port);
        var receiverSocket = await listener.AcceptAsync();
        await connectTask;
        listener.Dispose();

        var sender = new MoongateTcpClient(senderSocket);
        var receiver = new MoongateTcpClient(receiverSocket);

        await sender.StartAsync(CancellationToken.None);
        await receiver.StartAsync(CancellationToken.None);

        return (sender, receiver);
    }
}
