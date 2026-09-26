using System.Net;
using System.Net.Sockets;
using Moongate.Network.Server;

namespace Moongate.Network.Tests.Integration.Server;

/// <summary>
///     Everything the accept loop does after
///     <c>
///         AcceptAsync
///     </c>
///     can throw: a connection pipeline factory
///     can fail, or a client may not start. The socket accepted moments
///     earlier must not survive that. The peer would otherwise sit in an ESTABLISHED connection nobody
///     serves, waiting for a FIN that never comes, and the file descriptor would stay taken until a
///     finalizer happens to run — which nothing makes urgent, because descriptor pressure is invisible to
///     the garbage collector. Every wait here is bounded so a regression fails fast instead of hanging
///     the suite.
/// </summary>
public sealed class AcceptLoopConnectionReleaseTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AcceptLoop_ConnectHandlerThrowsAfterRegistration_ClosesTheAcceptedConnection()
    {
        // Arrange
        // OnClientConnect is raised from inside the client's StartAsync, so throwing from it fails
        // the loop at the one point where the client already exists and is already registered. The
        // release must dispose the client that owns the socket rather than the socket underneath it,
        // and must drop the registration the started client would otherwise have cleaned up itself.
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));

        server.OnClientConnect += (_, _) => throw new InvalidOperationException("connect handler failed");
        server.OnException += (_, _) => failed.TrySetResult();

        await server.StartAsync(CancellationToken.None);

        using var peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            // Act
            await peer.ConnectAsync(IPAddress.Loopback, server.Port);
            await failed.Task.WaitAsync(Timeout);

            // Assert
            Assert.Equal(0, await ReceiveWithTimeoutAsync(peer));
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task AcceptLoop_ConnectionPipelineFactoryThrows_ClosesTheAcceptedConnection()
    {
        // Arrange
        // The factory runs once the accept has already succeeded, so the connection is established
        // and owned by the loop at the moment the failure happens.
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new MoongateTcpServer(
            new(IPAddress.Loopback, 0),
            connectionPipelineFactory: () => throw new InvalidOperationException("pipeline factory failed")
        );

        server.OnException += (_, _) => failed.TrySetResult();

        await server.StartAsync(CancellationToken.None);

        using var peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            // Act
            await peer.ConnectAsync(IPAddress.Loopback, server.Port);
            await failed.Task.WaitAsync(Timeout);

            // Assert
            // A receive of zero bytes is the peer seeing FIN, which is the only externally visible
            // proof that the server let the connection go. While the server still holds the accepted
            // socket the receive simply never completes, so a regression hits the timeout below.
            Assert.Equal(0, await ReceiveWithTimeoutAsync(peer));
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    private static async Task<int> ReceiveWithTimeoutAsync(Socket socket)
    {
        using var cancellation = new CancellationTokenSource(Timeout);

        try
        {
            return await socket.ReceiveAsync(new byte[1], SocketFlags.None, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"The peer saw no close within {Timeout.TotalSeconds:F0}s, so the accept loop is " +
                "leaking a connection it accepted but never started."
            );
        }
    }
}
