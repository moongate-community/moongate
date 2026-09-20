using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Server.Services.GameLoop.Internal;

/// <summary>A single pending synchronous capture slot, usable only during one terminal callback.</summary>
internal sealed class GameLoopFinalWorkSession
{
    private readonly Lock _gate = new();
    private readonly Action _wake;
    private readonly CancellationToken _cancellationToken;
    private IGameLoopWorkItem? _pending;
    private TaskCompletionSource? _dispatch;
    private bool _closed;
    public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public GameLoopFinalWorkSession(Action wake, CancellationToken cancellationToken)
    {
        _wake = wake;
        _cancellationToken = cancellationToken;
    }

    public Task DispatchAsync(IGameLoopWorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_gate)
        {
            if (_closed || _dispatch is { Task.IsCompleted: false })
            {
                throw new InvalidOperationException("Final capture dispatch is closed or already has an active capture.");
            }
            _cancellationToken.ThrowIfCancellationRequested();
            _dispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = item;
            _wake();
            return _dispatch.Task;
        }
    }

    public bool ExecutePending()
    {
        IGameLoopWorkItem? item;
        TaskCompletionSource? completion;
        lock (_gate)
        {
            item = _pending;
            completion = _dispatch;
            _pending = null;
            if (item is null) { return !_closed; }
        }
        try { item.Execute(); completion!.TrySetResult(); }
        catch (Exception exception) { completion!.TrySetException(exception); }
        return true;
    }

    public async Task CloseAsync()
    {
        Task? active;
        bool returnedEarly;
        lock (_gate)
        {
            _closed = true;
            active = _dispatch?.Task;
            returnedEarly = active is { IsCompleted: false };
            _wake();
        }
        if (active is not null)
        {
            await active.ConfigureAwait(false);
        }
        if (returnedEarly)
        {
            throw new InvalidOperationException("The final callback returned before awaiting its admitted capture.");
        }
    }

    public void Fail(Exception failure)
    {
        lock (_gate)
        {
            _closed = true;
            _dispatch?.TrySetException(failure);
            Ready.TrySetException(failure);
        }
    }
}
