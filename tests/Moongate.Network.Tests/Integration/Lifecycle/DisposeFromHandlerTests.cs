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

    [Fact]
    public async Task ServerDispose_CalledFromDataReceivedHandler_ExternalDisposeDrainsCallback()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));
        MoongateTcpClient? accepted = null;
        server.OnDataReceived += (_, args) =>
        {
            accepted = args.Client;
            server.Dispose();
            disposed.TrySetResult();
            if (!release.Wait(Timeout))
            {
                throw new TimeoutException("Data handler was not released.");
            }
        };
        await server.StartAsync(CancellationToken.None);
        using var peer = new TcpClient();
        try
        {
            await peer.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(Timeout);
            await peer.GetStream().WriteAsync(new byte[] { 4, 5, 6 });
            await disposed.Task.WaitAsync(Timeout);
            var cleanup = server.DisposeAsync().AsTask();
            Assert.False(cleanup.IsCompleted);
        }
        finally
        {
            release.Set();
            await server.DisposeAsync().AsTask().WaitAsync(Timeout);
        }
        Assert.NotNull(accepted);
        Assert.False(accepted.IsConnected);
        Assert.False(server.IsRunning);
        Assert.Equal(0, server.Port);
        Assert.Equal(0, await peer.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(Timeout));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => server.StartAsync(CancellationToken.None));
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
