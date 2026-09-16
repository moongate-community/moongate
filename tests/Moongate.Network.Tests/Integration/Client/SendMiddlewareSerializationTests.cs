using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Interfaces.Middleware;
using Moongate.Network.Tests.Support;

namespace Moongate.Network.Tests.Integration.Client;

/// <summary>
/// The send path must be serialized per connection end to end. A protocol that encrypts only part of
/// a packet header has nowhere but a send middleware to do it, so a
/// stateful send middleware must consume its keystream in exactly the order the bytes hit the wire.
/// </summary>
public sealed class SendMiddlewareSerializationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private int _decodePosition;

    [Fact]
    public async Task SendAsync_SecondSendOvertakingTheFirst_DoesNotDesyncTheMiddlewareKeystream()
    {
        // Arrange
        // The middleware takes its keystream, then stalls before returning. A send path that runs the
        // middleware outside the send lock lets the second payload take the later keystream slice and
        // still reach the socket first, so the peer decodes it against the wrong slice.
        var middleware = new GatedKeystreamMiddleware();
        var frames = new BlockingCollection<byte[]>();
        var (sender, receiver) = await ConnectedPairAsync(middleware);
        receiver.OnDataReceived += (_, e) => frames.Add(e.Data.ToArray());

        var first = new byte[] { 0xA1, 0xA1, 0xA1, 0xA1 };
        var second = new byte[] { 0xB2, 0xB2, 0xB2, 0xB2 };

        try
        {
            // Act
            var firstSend = sender.SendAsync(first, CancellationToken.None);
            await middleware.KeystreamTaken.Task.WaitAsync(Timeout);

            var secondSend = sender.SendAsync(second, CancellationToken.None);

            // Long enough for an unserialized second send to run the middleware and reach the socket.
            await Task.Delay(250);
            middleware.Release();
            await Task.WhenAll(firstSend, secondSend).WaitAsync(Timeout);

            // Assert
            Assert.Equal(first, DecodeNext(frames).Payload);
            Assert.Equal(second, DecodeNext(frames).Payload);
        }
        finally
        {
            await receiver.DisposeAsync();
            await sender.DisposeAsync();
        }
    }

    [Fact]
    public async Task SendAsync_ConcurrentSends_PreserveMiddlewareKeystreamIntegrity()
    {
        // Arrange
        const int messageCount = 40;
        var frames = new BlockingCollection<byte[]>();
        var (sender, receiver) = await ConnectedPairAsync(new YieldingKeystreamMiddleware());
        receiver.OnDataReceived += (_, e) => frames.Add(e.Data.ToArray());

        try
        {
            // Act
            await Parallel.ForEachAsync(
                Enumerable.Range(0, messageCount),
                async (id, ct) => await sender.SendAsync(new byte[] { (byte)id, (byte)id, (byte)id, (byte)id }, ct)
            );

            // Assert
            // Every frame carries its id in clear next to the same id enciphered. A keystream consumed
            // in a different order than the wire order decodes the body against the wrong slice, so
            // the body stops agreeing with the tag whatever order the frames themselves arrive in.
            var ids = new HashSet<byte>();

            for (var i = 0; i < messageCount; i++)
            {
                var (tag, payload) = DecodeNext(frames);
                Assert.Equal(4, payload.Length);
                Assert.All(payload, actual => Assert.Equal(tag, actual));
                Assert.True(ids.Add(tag), $"Duplicate payload id {tag}.");
            }

            Assert.Equal(messageCount, ids.Count);
        }
        finally
        {
            await receiver.DisposeAsync();
            await sender.DisposeAsync();
        }
    }

    /// <summary>
    /// Takes the next framed message and reverses the middleware keystream with a receive-side
    /// position that advances in arrival order, the way a real peer's decryptor does.
    /// </summary>
    private (byte Tag, byte[] Payload) DecodeNext(BlockingCollection<byte[]> frames)
    {
        Assert.True(frames.TryTake(out var frame, Timeout));

        var payload = frame.AsSpan(2).ToArray();

        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] ^= (byte)_decodePosition++;
        }

        return (frame[1], payload);
    }

    private static async Task<(MoongateTcpClient Sender, MoongateTcpClient Receiver)> ConnectedPairAsync(
        INetMiddleware middleware
    )
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

        var sender = new MoongateTcpClient(senderSocket, [middleware]);
        var receiver = new MoongateTcpClient(receiverSocket, middlewares: null, new LengthPrefixFramer(), null);

        await sender.StartAsync(CancellationToken.None);
        await receiver.StartAsync(CancellationToken.None);

        return (sender, receiver);
    }

}
