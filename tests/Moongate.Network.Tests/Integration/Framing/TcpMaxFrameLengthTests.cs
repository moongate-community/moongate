using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Tests.Support;

namespace Moongate.Network.Tests.Integration.Framing;

public sealed class TcpMaxFrameLengthTests
{
    [Fact]
    public async Task FrameAtTheLimit_IsDelivered()
    {
        var (server, client) = await ConnectedPairAsync(1024);
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.OnDataReceived += (_, e) => received.TrySetResult(e.Data.ToArray());

        try
        {
            var payload = new byte[1020];
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteInt32BigEndian(frame, payload.Length);
            payload.CopyTo(frame, 4);
            await server.SendAsync(frame, CancellationToken.None);

            var got = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(4 + payload.Length, got.Length);
            Assert.True(client.IsConnected);
        }
        finally
        {
            await client.DisposeAsync();
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task OversizedDeclaredFrame_ClosesConnection()
    {
        var (server, client) = await ConnectedPairAsync(1024);

        try
        {
            // Declare a 10 MiB payload (way over the 1 KiB cap) then dribble bytes; the receiver must
            // close instead of growing its pending buffer toward 10 MiB.
            var header = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(header, 10 * 1024 * 1024);
            await server.SendAsync(header, CancellationToken.None);
            await server.SendAsync(new byte[4096], CancellationToken.None);

            var closed = await WaitUntilAsync(() => !client.IsConnected, TimeSpan.FromSeconds(5));
            Assert.True(closed);
        }
        finally
        {
            await client.DisposeAsync();
            await server.DisposeAsync();
        }
    }

    private static async Task<(MoongateTcpClient Server, MoongateTcpClient Client)> ConnectedPairAsync(int maxFrameLength)
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        var port = ((IPEndPoint)listener.LocalEndPoint!).Port;

        var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var connectTask = clientSocket.ConnectAsync(IPAddress.Loopback, port);
        var serverSocket = await listener.AcceptAsync();
        await connectTask;
        listener.Dispose();

        // "server" side here is just the sending end; the receiving end (client) enforces the cap.
        var server = new MoongateTcpClient(serverSocket);
        var client = new MoongateTcpClient(
            clientSocket,
            null,
            new FourByteLengthPrefixFramer(),
            maxFrameLength: maxFrameLength
        );

        await server.StartAsync(CancellationToken.None);
        await client.StartAsync(CancellationToken.None);

        return (server, client);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(25);
        }

        return condition();
    }
}
