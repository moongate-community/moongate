using System.Net;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Interfaces.Framing;
using Moongate.Network.Tests.Support;

namespace Moongate.Network.Tests.Integration.Framing;

public sealed class FramerContractTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Theory, InlineData(0), InlineData(-1), InlineData(4096)]
    public async Task BogusFrameLength_ClosesTheConnection(int reportedLength)
    {
        // Arrange
        // A framer that decrypts a header in place and then reports a nonsensical length has already
        // advanced its keystream over those bytes. Silently dropping the buffer would leave the
        // connection open with a permanently desynchronised framer, so the transport must reject it.
        var (sender, receiver) = await ConnectedPairAsync(new BogusLengthFramer(reportedLength));
        var failure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.OnException += (_, e) => failure.TrySetResult(e.Exception);

        try
        {
            // Act
            await sender.SendAsync(new byte[] { 1, 2, 3, 4 }, CancellationToken.None);

            // Assert
            var raised = await failure.Task.WaitAsync(Timeout);
            Assert.IsType<InvalidDataException>(raised);
            Assert.True(await WaitUntilAsync(() => !receiver.IsConnected, Timeout));
        }
        finally
        {
            await receiver.DisposeAsync();
            await sender.DisposeAsync();
        }
    }

    private static async Task<(MoongateTcpClient Sender, MoongateTcpClient Receiver)> ConnectedPairAsync(INetFramer framer)
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
        var receiver = new MoongateTcpClient(receiverSocket, null, framer);

        await sender.StartAsync(CancellationToken.None);
        await receiver.StartAsync(CancellationToken.None);

        return (sender, receiver);
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
