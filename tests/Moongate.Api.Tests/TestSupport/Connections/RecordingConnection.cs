using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Moongate.Network.Interfaces.Client;
using Moongate.Network.Interfaces.Framing;

namespace Moongate.Api.Tests.TestSupport.Connections;

internal sealed class RecordingConnection : INetworkConnection
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ConcurrentQueue<byte[]> Sent { get; } = new();

    public Channel<byte[]> Written { get; } =
        Channel.CreateUnbounded<byte[]>();

    public TaskCompletionSource SendEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource? SendGate { get; set; }
    public long SessionId => 1;
    public EndPoint? RemoteEndPoint => null;
    public bool IsConnected => !_completion.Task.IsCompleted;
    public INetFramer? Framer => null;
    public Task Completion => _completion.Task;

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _completion.TrySetResult();

        return Task.CompletedTask;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        SendEntered.TrySetResult();

        if (SendGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var copy = payload.ToArray();
        Sent.Enqueue(copy);
        Written.Writer.TryWrite(copy);
    }
}
