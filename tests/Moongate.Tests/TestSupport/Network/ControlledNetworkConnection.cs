using System.Net;
using System.Threading.Channels;
using Moongate.Network.Interfaces.Client;
using Moongate.Network.Interfaces.Framing;

namespace Moongate.Tests.TestSupport.Network;

internal sealed class ControlledNetworkConnection : INetworkConnection, IDisposable
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _closeRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<byte[]> _sent = Channel.CreateUnbounded<byte[]>();
    private readonly TaskCompletionSource _sendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _connected = 1;
    private int _closeCalls;

    public long SessionId { get; }
    public EndPoint? RemoteEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, 5151);
    public EndPoint? LocalEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, 2593);
    public bool IsConnected => Volatile.Read(ref _connected) != 0;
    public INetFramer? Framer => null;
    public Task Completion => _completion.Task;
    public Task CloseRequested => _closeRequested.Task;
    public Task SendStarted => _sendStarted.Task;
    public Task? SendGate { get; init; }
    public Exception? SendFailure { get; init; }
    public bool CompleteOnSendFailure { get; init; }
    public Task? SendFailureDeliveryGate { get; init; }
    public int CloseCalls => Volatile.Read(ref _closeCalls);
    public bool DelayCompletion { get; init; }
    public bool DelayDisconnectionState { get; init; }
    public Task? CloseGate { get; init; }
    public Exception? CloseFailure { get; init; }

    public ControlledNetworkConnection(long sessionId)
    {
        SessionId = sessionId;
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _closeCalls);
        _closeRequested.TrySetResult();

        if (!DelayDisconnectionState)
        {
            Interlocked.Exchange(ref _connected, 0);
        }

        if (CloseGate is not null)
        {
            await CloseGate.WaitAsync(cancellationToken);
        }

        if (CloseFailure is not null)
        {
            throw CloseFailure;
        }

        if (!DelayCompletion)
        {
            Complete();
        }
    }

    public void Complete(Exception? failure = null)
    {
        Interlocked.Exchange(ref _connected, 0);

        if (failure is null)
        {
            _completion.TrySetResult();
        }
        else
        {
            _completion.TrySetException(failure);
        }
    }

    public void Dispose()
    {
        Complete();
        _sent.Writer.TryComplete();
    }

    public Task<byte[]> ReadSentAsync(CancellationToken token)
        => _sent.Reader.ReadAsync(token).AsTask();

    public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsConnected)
        {
            throw new IOException("Connection is closed.");
        }

        _sendStarted.TrySetResult();

        if (SendGate is not null)
        {
            await SendGate.WaitAsync(cancellationToken);
        }

        if (SendFailure is not null)
        {
            if (CompleteOnSendFailure)
            {
                Complete();
            }

            if (SendFailureDeliveryGate is not null)
            {
                await SendFailureDeliveryGate;
            }

            throw SendFailure;
        }

        _sent.Writer.TryWrite(payload.ToArray());
    }
}
