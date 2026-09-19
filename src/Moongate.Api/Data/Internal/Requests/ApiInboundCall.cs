using Moongate.Api.Data.Internal.Protocol;
using Moongate.Api.Interfaces.Internal.Registry;

namespace Moongate.Api.Data.Internal.Requests;

internal sealed class ApiInboundCall : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private ITimer? _timer;
    private Task _cancelled = Task.CompletedTask;
    private int _terminal;
    private bool _disposed;
    public ApiEnvelope Envelope { get; }
    public IApiOperationRegistration Operation { get; }
    public CancellationToken Token => _cancellation.Token;
    public bool IsTerminal => Volatile.Read(ref _terminal) != 0;

    public ApiInboundCall(ApiEnvelope envelope, IApiOperationRegistration operation)
    {
        Envelope = envelope;
        Operation = operation;
    }

    public void Arm(TimeProvider clock, TimeSpan remaining, Action<ApiInboundCall> expired)
    {
        var timer = clock.CreateTimer(
            _ => expired(this),
            null,
            remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
            Timeout.InfiniteTimeSpan
        );

        lock (_gate)
        {
            if (_disposed || IsTerminal) { timer.Dispose(); }
            else { _timer = timer; }
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            if (!_disposed && !_cancellation.IsCancellationRequested) { _cancelled = _cancellation.CancelAsync(); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task cancelled;

        lock (_gate)
        {
            if (_disposed) { return; }
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
            cancelled = _cancelled;
        }

        try { await cancelled.ConfigureAwait(false); }
        finally { _cancellation.Dispose(); }
    }

    public bool TryFinish()
    {
        if (Interlocked.CompareExchange(ref _terminal, 1, 0) != 0) { return false; }

        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }

        return true;
    }
}
