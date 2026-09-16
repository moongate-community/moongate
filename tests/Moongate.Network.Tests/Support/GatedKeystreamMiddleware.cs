using Moongate.Network.Client;
using Moongate.Network.Interfaces.Middleware;

namespace Moongate.Network.Tests.Support;

public sealed class GatedKeystreamMiddleware : INetMiddleware
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _invocations;
    private int _position;

    public bool IgnoreCancellationWhileHeld { get; set; }
    public Exception? SendFailure { get; set; }

    public TaskCompletionSource SecondEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource KeystreamTaken { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public GatedKeystreamMiddleware()
    {
    }

    public void Release()
        => _gate.TrySetResult();

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
        var frame = BuildKeystreamFrame(data.Span, ref _position);

        if (Interlocked.Increment(ref _invocations) == 1)
        {
            KeystreamTaken.TrySetResult();
            await _gate.Task.WaitAsync(IgnoreCancellationWhileHeld ? CancellationToken.None : cancellationToken);
        }

        else
        {
            SecondEntered.TrySetResult();
        }

        if (SendFailure is not null)
        {
            throw SendFailure;
        }

        return frame;
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
