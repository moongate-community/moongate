using System.Threading.Channels;
using Moongate.Network.Interfaces.Client;

namespace Moongate.Server.Services.Packets.Internal;

internal sealed class SessionPacketOutbox
{
    private readonly Channel<byte[]> _queue;
    private readonly Func<long, Task> _disconnect;
    private readonly Task _disconnectRequested;
    private readonly TaskCompletionSource _closure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _admissionGate = new();
    private byte[]? _terminalFrame;
    private int _closed;
    private int _closeRequested;
    private int _terminalQueued;
    private int _terminalSent;

    public INetworkConnection Connection { get; }
    public Task Completion { get; private set; } = Task.CompletedTask;
    public bool TerminalQueued => Volatile.Read(ref _terminalQueued) != 0;
    public bool TerminalSent => Volatile.Read(ref _terminalSent) != 0;

    public SessionPacketOutbox(
        INetworkConnection connection,
        Task disconnectRequested,
        int capacity,
        Func<long, Task> disconnect
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

    public void Close()
    {
        CloseQueue();

        if (Interlocked.Exchange(ref _closeRequested, 1) == 0)
        {
            _ = CloseConnectionAsync();
        }
    }

    public void Start()

        // Middleware can block synchronously: one worker per connection, never per packet.
        => Completion = Task.Run(RunAsync);

    public bool TryWrite(byte[] frame)
    {
        lock (_admissionGate)
        {
            return _closed == 0 && _terminalFrame is null && _queue.Writer.TryWrite(frame);
        }
    }

    public bool TryWriteTerminal(byte[] frame)
    {
        lock (_admissionGate)
        {
            if (_closed != 0 || _terminalFrame is not null)
            {
                return false;
            }

            _terminalFrame = frame;
            Volatile.Write(ref _terminalQueued, 1);
            _queue.Writer.TryComplete();

            return true;
        }
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

    private void CloseQueue()
    {
        lock (_admissionGate)
        {
            Volatile.Write(ref _closed, 1);
            _queue.Writer.TryComplete();
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

            if (!await SendFrameAsync(frame).ConfigureAwait(false))
            {
                break;
            }
        }

        if (_terminalFrame is not null &&
            Volatile.Read(ref _closed) == 0 &&
            Connection.IsConnected &&
            await SendFrameAsync(_terminalFrame).ConfigureAwait(false))
        {
            Volatile.Write(ref _terminalSent, 1);
        }
    }

    private async Task<bool> SendFrameAsync(byte[] frame)
    {
        try
        {
            await Connection.SendAsync(frame, CancellationToken.None).ConfigureAwait(false);

            return true;
        }
        catch (Exception exception) when (_disconnectRequested.IsCompletedSuccessfully &&
                                          exception is IOException or
                                                       ObjectDisposedException or
                                                       OperationCanceledException)
        {
            // Only the captured owner request identifies an intentionally interrupted write.
            // A send failure may close the transport itself, so its current state is not a cause.
            return false;
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

        while (_queue.Reader.TryRead(out _)) { }

        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
    }
}
