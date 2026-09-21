using Moongate.Api.Data.Config;
using Moongate.Api.Data.Internal.Requests;
using Moongate.Network.Interfaces.Client;

namespace Moongate.Api.Connections.Internal;

internal sealed class ApiOutbox : IAsyncDisposable
{
    private readonly INetworkConnection _transport;
    private readonly int _capacity;
    private readonly TimeSpan _writeTimeout;
    private readonly TimeProvider _clock;
    private readonly LinkedList<ApiOutboundFrame> _queue = new();
    private readonly Dictionary<uint, LinkedListNode<ApiOutboundFrame>> _requests = [];
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly CancellationTokenSource _stop = new();
    private bool _completed;
    private Exception? _failure;
    public object SyncRoot { get; } = new();
    public Task Completion { get; }

    public ApiOutbox(INetworkConnection transport, int capacity, TimeSpan writeTimeout, TimeProvider clock)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ApiOptions.ValidateTimeout(writeTimeout);
        _transport = transport;
        _capacity = capacity;
        _writeTimeout = writeTimeout;
        _clock = clock;
        Completion = WriteAsync();
    }

    public void Abort(Exception error)
    {
        lock (SyncRoot)
        {
            _failure ??= error;
            _completed = true;
            _queue.Clear();
            _requests.Clear();
            Signal();
        }

        _stop.Cancel();
    }

    public void Complete()
    {
        lock (SyncRoot)
        {
            _completed = true;
            Signal();
        }
    }

    public bool TryEnqueue(ApiOutboundFrame frame)
    {
        lock (SyncRoot)
        {
            if (_completed || _queue.Count >= _capacity)
            {
                return false;
            }

            var node = _queue.AddLast(frame);

            if (frame.LocalRequestId is { } id)
            {
                _requests.Add(id, node);
            }

            Signal();

            return true;
        }
    }

    public bool TryRemove(uint outgoingRequestId)
    {
        lock (SyncRoot)
        {
            if (!_requests.Remove(outgoingRequestId, out var node))
            {
                return false;
            }

            _queue.Remove(node);

            return true;
        }
    }

    private void Signal()
    {
        if (_signal.CurrentCount == 0)
        {
            _signal.Release();
        }
    }

    private async Task WriteAsync()
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        try
        {
            while (true)
            {
                bool wait;

                lock (SyncRoot)
                {
                    wait = _queue.Count == 0 && !_completed;
                }

                if (wait)
                {
                    await _signal.WaitAsync(_stop.Token).ConfigureAwait(false);
                }

                _stop.Token.ThrowIfCancellationRequested();
                ApiOutboundFrame? frame;

                lock (SyncRoot)
                {
                    if (_queue.First is not { } node)
                    {
                        if (_completed)
                        {
                            break;
                        }

                        continue;
                    }

                    frame = node.Value;
                    _queue.RemoveFirst();

                    if (frame.LocalRequestId is { } id)
                    {
                        _requests.Remove(id);
                    }
                }

                using var deadline = new CancellationTokenSource(_writeTimeout, _clock);
                using var sendToken = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token, deadline.Token);

                try
                {
                    await _transport.SendAsync(frame.Bytes, sendToken.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception) when (deadline.IsCancellationRequested &&
                                                                   !_stop.IsCancellationRequested)
                {
                    throw new TimeoutException("The API write deadline elapsed.", exception);
                }
            }

            if (_failure is { } failure)
            {
                throw failure;
            }
        }
        catch (Exception exception)
        {
            lock (SyncRoot)
            {
                _failure ??= exception;
                _completed = true;
                _queue.Clear();
                _requests.Clear();
            }

            await _transport.CloseAsync().ConfigureAwait(false);

            throw _failure;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Complete();

        try
        {
            await Completion.ConfigureAwait(false);
        }
        finally
        {
            _signal.Dispose();
            _stop.Dispose();
        }
    }
}
