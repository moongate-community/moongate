using Moongate.Network.Client;
using Moongate.Network.Interfaces.Middleware;

namespace Moongate.Network.Tests.Support;

public sealed class YieldingKeystreamMiddleware : INetMiddleware
{
    private int _position;

    public ValueTask<ReadOnlyMemory<byte>> ProcessAsync(
        MoongateTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
        => ValueTask.FromResult(data);

    public async ValueTask<ReadOnlyMemory<byte>> ProcessSendAsync(
        MoongateTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        await Task.Yield();

        return BuildKeystreamFrame(data.Span, ref _position);
    }

    private static byte[] BuildKeystreamFrame(ReadOnlySpan<byte> payload, ref int position)
    {
        var frame = new byte[2 + payload.Length];
        frame[0] = (byte)(1 + payload.Length);
        frame[1] = payload[0];

        for (var i = 0; i < payload.Length; i++)
        {
            frame[2 + i] = (byte)(payload[i] ^ (byte)position++);
        }

        return frame;
    }
}
