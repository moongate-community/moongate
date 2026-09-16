using System.Net;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Interfaces.Codecs;
using Moongate.Network.Interfaces.Framing;
using Moongate.Network.Interfaces.Middleware;

namespace Moongate.Network.Tests.Support;

public sealed class LoopbackPair : IAsyncDisposable
{
    public MoongateTcpClient Sender { get; }
    public MoongateTcpClient Receiver { get; }

    private LoopbackPair(MoongateTcpClient sender, MoongateTcpClient receiver)
    {
        Sender = sender;
        Receiver = receiver;
    }

    public static async Task<LoopbackPair> CreateAsync(
        Stream? senderStream = null,
        ITransportCodec? codec = null,
        bool startSender = true,
        IEnumerable<INetMiddleware>? receiverMiddlewares = null,
        INetFramer? receiverFramer = null,
        int receiverBufferSize = 8192,
        int receiverMaxFrameLength = 1024 * 1024
    )
    {
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        Socket? senderSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Socket? receiverSocket = null;
        MoongateTcpClient? sender = null;
        MoongateTcpClient? receiver = null;
        try
        {
            var connect = senderSocket.ConnectAsync(listener.LocalEndPoint!);
            receiverSocket = await listener.AcceptAsync();
            await connect;
            sender = senderStream is null
                ? new MoongateTcpClient(senderSocket, codec: codec)
                : new MoongateTcpClient(senderSocket, senderStream, codec: codec);
            senderSocket = null;
            receiver = new MoongateTcpClient(
                receiverSocket,
                receiverMiddlewares,
                receiverFramer,
                receiveBufferSize: receiverBufferSize,
                maxFrameLength: receiverMaxFrameLength
            );
            receiverSocket = null;
            if (startSender)
            {
                await sender.StartAsync(CancellationToken.None);
            }
            await receiver.StartAsync(CancellationToken.None);
            return new LoopbackPair(sender, receiver);
        }
        catch
        {
            try
            {
                if (sender is not null)
                {
                    await sender.DisposeAsync();
                }
            }
            finally
            {
                if (receiver is not null)
                {
                    await receiver.DisposeAsync();
                }
            }
            throw;
        }
        finally
        {
            senderSocket?.Dispose();
            receiverSocket?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Sender.CloseAsync();
        await Receiver.CloseAsync();
        try
        {
            await Sender.DisposeAsync();
        }
        finally
        {
            await Receiver.DisposeAsync();
        }
    }
}
