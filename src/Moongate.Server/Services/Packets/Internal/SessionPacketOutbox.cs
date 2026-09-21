using System.Threading.Channels;
using Moongate.Network.Interfaces.Client;

namespace Moongate.Server.Services.Packets.Internal;

internal sealed class SessionPacketOutbox
{
    private readonly Channel<byte[]> _queue;
    private readonly Func<long, Task> _disconnect;
    private readonly Task _disconnectRequested;
    private readonly TaskCompletionSource _closure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _closed;
    private int _closeRequested;

    public INetworkConnection Connection { get; }
    public Task Completion { get; private set; } = Task.CompletedTask;

    public SessionPacketOutbox(
        INetworkConnection connection, Task disconnectRequested, int capacity, Func<long, Task> disconnect
    )
    {
        Connection = connection;
        _disconnectRequested = disconnectRequested;
        _disconnect = disconnect;
        _queue = Channel.CreateBounded<byte[]>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            }
        );
    }

    public void Start()
    {
        // Middleware can block synchronously: one worker per connection, never per packet.
        Completion = Task.Run(RunAsync);
    }

    public bool TryWrite(byte[] frame)
    {
        return Volatile.Read(ref _closed) == 0 && _queue.Writer.TryWrite(frame);
    }

    public void Close()
    {
        CloseQueue();
        if (Interlocked.Exchange(ref _closeRequested, 1) == 0)
        {
            _ = CloseConnectionAsync();
        }
    }

    private void CloseQueue()
    {
        Interlocked.Exchange(ref _closed, 1);
        _queue.Writer.TryComplete();
    }

    private async Task CloseConnectionAsync()
    {
        try
        {
            // The registry closes admission synchronously before yielding for transport cleanup.
            await _disconnect(Connection.SessionId).ConfigureAwait(false);
            _closure.TrySetResult();
        }
        catch (Exception exception)
        {
            _closure.TrySetException(exception);
        }
    }

    private async Task RunAsync()
    {
        List<Exception> failures = [];
        var drain = DrainAsync();
        await Task.WhenAny(drain, Connection.Completion).ConfigureAwait(false);
        CloseQueue();
        try
        {
            await drain.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        // Classify the send result before automatic failure cleanup can publish a close request.
        Close();
        try
        {
            await _closure.Task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        while (_queue.Reader.TryRead(out _))
        {
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
    }

    private async Task DrainAsync()
    {
        await foreach (var frame in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (Volatile.Read(ref _closed) != 0 || !Connection.IsConnected)
            {
                break;
            }

            try
            {
                await Connection.SendAsync(frame, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (_disconnectRequested.IsCompletedSuccessfully &&
                                              exception is IOException or ObjectDisposedException
                                                  or OperationCanceledException)
            {
                // Only the captured owner request identifies an intentionally interrupted write.
                // A send failure may close the transport itself, so its current state is not a cause.
                break;
            }
        }
    }
}
