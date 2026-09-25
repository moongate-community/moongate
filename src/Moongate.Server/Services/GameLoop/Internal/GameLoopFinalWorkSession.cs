using System.Runtime.ExceptionServices;
using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Server.Services.GameLoop.Internal;

/// <summary>
///     A single pending synchronous capture slot, usable only during one terminal callback.
/// </summary>
internal sealed class GameLoopFinalWorkSession
{
    private readonly Lock _gate = new();
    private readonly Action _wake;
    private readonly List<Exception> _failures = [];
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

    public Task CloseAsync()
    {
        return CloseAsync(null);
    }

    public async Task CloseAsync(Exception? callbackFailure)
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
            try
            {
                await active.ConfigureAwait(false);
            }
            catch (Exception)
            {
                /* Capture failures were retained before completing the dispatch. */
            }
        }

        List<Exception> failures = [];

        if (callbackFailure is not null)
        {
            failures.Add(callbackFailure);
        }

        lock (_gate)
        {
            foreach (var failure in _failures)
            {
                if (!failures.Any(existing => ReferenceEquals(existing, failure)))
                {
                    failures.Add(failure);
                }
            }
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(failures);
        }

        if (returnedEarly)
        {
            throw new InvalidOperationException("The final callback returned before awaiting its admitted capture.");
        }
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
            _dispatch = new(TaskCreationOptions.RunContinuationsAsynchronously);
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

            if (item is null)
            {
                return !_closed;
            }
        }

        try
        {
            item.Execute();
            completion!.TrySetResult();
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                _failures.Add(exception);
            }

            completion!.TrySetException(exception);
        }

        return true;
    }

    public void Fail(Exception failure)
    {
        lock (_gate)
        {
            _closed = true;
            _failures.Add(failure);
            _dispatch?.TrySetException(failure);
            Ready.TrySetException(failure);
        }
    }
}
