using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Tests.Support;

namespace Moongate.Network.Tests.Integration.Framing;

public sealed class StatefulFramerTests
{
    [Fact]
    public async Task TryReadFrame_FrameArrivesInOneWrite_DecodesTheHeaderOnce()
    {
        // Arrange
        var (sender, receiver, received) =
            await ConnectedPairAsync();

        try
        {
            byte key = 0;
            byte[] payload = [1, 2, 3, 4, 5, 6, 7, 8];
            var frame = StatefulHeaderFramer.Encode(payload, ref key);

            // Act
            await sender.SendAsync(frame, CancellationToken.None);
            var got = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            Assert.Equal(StatefulHeaderFramer.HeaderLength + payload.Length, got.Length);
            Assert.Equal(payload.Length, BinaryPrimitives.ReadInt32BigEndian(got));
            Assert.Equal(payload, got[StatefulHeaderFramer.HeaderLength..]);
        }
        finally
        {
            await receiver.DisposeAsync();
            await sender.DisposeAsync();
        }
    }

    [Fact]
    public async Task TryReadFrame_FrameDribbledOneByteAtATime_ProducesTheSameFrame()
    {
        // Arrange
        // The receiver's framer is invoked after every single byte, so a header transformed more
        // than once would advance the running key and yield a different length.
        var (sender, receiver, received) =
            await ConnectedPairAsync();

        try
        {
            byte key = 0;
            byte[] payload = [1, 2, 3, 4, 5, 6, 7, 8];
            var frame = StatefulHeaderFramer.Encode(payload, ref key);

            // Act
            foreach (var b in frame)
            {
                await sender.SendAsync(new[] { b }, CancellationToken.None);
                await Task.Delay(5);
            }

            var got = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            Assert.Equal(StatefulHeaderFramer.HeaderLength + payload.Length, got.Length);
            Assert.Equal(payload.Length, BinaryPrimitives.ReadInt32BigEndian(got));
            Assert.Equal(payload, got[StatefulHeaderFramer.HeaderLength..]);
        }
        finally
        {
            await receiver.DisposeAsync();
            await sender.DisposeAsync();
        }
    }

    [Fact]
    public async Task TryReadFrame_TwoFramesBackToBack_KeepTheKeyInStep()
    {
        // Arrange
        // The running key carries across frames, the way a stream cipher's keystream does.
        var (sender, receiver, _) = await ConnectedPairAsync();

        var frames = new List<byte[]>();
        var second = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        receiver.OnDataReceived += (_, e) =>
                                   {
                                       frames.Add(e.Data.ToArray());

                                       if (frames.Count == 2)
                                       {
                                           second.TrySetResult(true);
                                       }
                                   };

        try
        {
            byte key = 0;
            var first = StatefulHeaderFramer.Encode([1, 2, 3], ref key);
            var next = StatefulHeaderFramer.Encode([4, 5, 6, 7, 8], ref key);

            // Act
            await sender.SendAsync(first, CancellationToken.None);
            await sender.SendAsync(next, CancellationToken.None);
            await second.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(frames[0]));
            Assert.Equal(5, BinaryPrimitives.ReadInt32BigEndian(frames[1]));
        }
        finally
        {
            await receiver.DisposeAsync();
            await sender.DisposeAsync();
        }
    }

    private static async Task<(MoongateTcpClient Sender, MoongateTcpClient Receiver, TaskCompletionSource<byte[]> Received)>
        ConnectedPairAsync()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);

        var port = ((IPEndPoint)listener.LocalEndPoint!).Port;

        var senderSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var connect = senderSocket.ConnectAsync(IPAddress.Loopback, port);
        var receiverSocket = await listener.AcceptAsync();
        await connect;
        listener.Dispose();

        var sender = new MoongateTcpClient(senderSocket);
        var receiver = new MoongateTcpClient(receiverSocket, null, new StatefulHeaderFramer());

        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.OnDataReceived += (_, e) => received.TrySetResult(e.Data.ToArray());

        await sender.StartAsync(CancellationToken.None);
        await receiver.StartAsync(CancellationToken.None);

        return (sender, receiver, received);
    }
}
