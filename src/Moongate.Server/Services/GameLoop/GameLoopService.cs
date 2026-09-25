using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.GameLoop.Internal;
using Moongate.Server.Services.GameLoop.Internal;
using Moongate.Server.Services.Timing;
using Moongate.Server.Types.GameLoop.Internal;
using Serilog;

namespace Moongate.Server.Services.GameLoop;

/// <summary>
///     Executes bounded, synchronous work on one dedicated thread.
/// </summary>
public sealed class GameLoopService : IGameLoopService, IDisposable
{
    private readonly Lock _gate = new();
    private readonly AsyncLocal<bool> _insideFinalWork = new();
    private readonly Channel<QueuedGameLoopWorkItem> _inbox;
    private readonly AutoResetEvent _wake;
    private readonly GameLoopPump _pump;
    private readonly TimeProvider _timeProvider;
    private readonly TimerWheelService _timers;
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ILogger _logger = Log.ForContext<GameLoopService>();

    private readonly GameLoopOptions _gameLoopOptions;

    private GameLoopState _state;
    private Thread? _thread;
    private Task? _stopTask;
    private Task? _finalStopTask;
    private GameLoopFinalWorkSession? _finalSession;
    private IGameLoopWorkItem? _finalWorkItem;
    private int _loopThreadId;
    private bool _disposed;
    private long _acceptedWorkItems;
    private long _rejectedWorkItems;
    private long _faults;

    /// <inheritdoc />
    public bool IsOnLoopThread => Volatile.Read(ref _loopThreadId) == Environment.CurrentManagedThreadId;

    /// <inheritdoc />
    public Task Completion => _completion.Task;

    /// <summary>
    ///     Creates a stopped inbox; StartAsync must complete before producers can post work.
    /// </summary>
    public GameLoopService(GameLoopOptions options, TimerWheelService timers, TimeProvider timeProvider)
    {
        _inbox = Channel.CreateBounded<QueuedGameLoopWorkItem>(
            new BoundedChannelOptions(options.QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            }
        );
        _gameLoopOptions = options;
        _timeProvider = timeProvider;
        _timers = timers;
        _pump = new(_inbox.Reader, options.MaxWorkItemsPerBatch, _timeProvider, options.WorkItemBudget);
        _wake = new(false);
    }

    /// <inheritdoc />
    public GameLoopMetricsSnapshot GetMetricsSnapshot()
    {
        lock (_gate)
        {
            var depth = _inbox.Reader.Count;
            var age = depth > 0 && _inbox.Reader.TryPeek(out var oldest)
                ? _timeProvider.GetElapsedTime(oldest.EnqueuedAt)
                : TimeSpan.Zero;

            return _pump.GetMetricsSnapshot() with
            {
                QueueDepth = depth,
                OldestQueuedItemAge = age,
                AcceptedWorkItems = _acceptedWorkItems,
                RejectedWorkItems = _rejectedWorkItems,
                Faults = _faults
            };
        }
    }

    /// <inheritdoc />
    public ValueTask PostAsync(IGameLoopWorkItem workItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        RejectLoopThreadWait();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            EnsureRunning();
        }

        return WaitForAdmissionAsync(workItem, cancellationToken);
    }

    /// <summary>
    ///     Starts the dedicated thread and completes only after its identity is established.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     This instance has already stopped and cannot restart.
    /// </exception>
    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_state is GameLoopState.Starting or GameLoopState.Running)
            {
                return _started.Task;
            }

            if (_state != GameLoopState.Created)
            {
                throw new InvalidOperationException("The game loop cannot be restarted after shutdown.");
            }

            _state = GameLoopState.Starting;

            try
            {
                var thread = new Thread(Run)
                {
                    Name = "Moongate Game Loop",
                    IsBackground = true
                };
                thread.Start();

                // The new thread cannot take _gate until its successful start is recorded.
                _thread = thread;

                _logger.Information(
                    "Started game loop thread {ThreadName} (ID {ThreadId}), max {MaxWorkItemsPerBatch} work items per batch",
                    thread.Name,
                    thread.ManagedThreadId,
                    _gameLoopOptions.MaxWorkItemsPerBatch
                );
            }
            catch (Exception exception)
            {
                _state = GameLoopState.Stopped;
                _faults++;
                _timers.Close();
                _inbox.Writer.TryComplete();
                _wake.Dispose();
                _disposed = true;
                _started.TrySetException(exception);
                _completion.TrySetException(exception);
            }

            return _started.Task;
        }
    }

    /// <summary>
    ///     Closes admission, drains accepted work and waits for the dedicated thread to exit.
    /// </summary>
    /// <remarks>
    ///     Cleanup succeeds after a handler fault; Completion retains the original failure for the host to observe.
    ///     Synchronous handlers cannot be preempted, so this operation has no forced shutdown timeout.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     The caller is executing on the loop thread.
    /// </exception>
    public Task StopAsync()
    {
        RejectLoopThreadWait();

        lock (_gate)
        {
            if (_stopTask is not null)
            {
                return _stopTask;
            }

            _timers.Close();

            if (_state == GameLoopState.Created)
            {
                _state = GameLoopState.Stopped;
                _inbox.Writer.TryComplete();
                _completion.TrySetResult();
            }
            else if (_state is GameLoopState.Starting or GameLoopState.Running)
            {
                _state = GameLoopState.Stopping;
                _inbox.Writer.TryComplete();
                _wake.Set();
            }

            _stopTask = WaitForThreadExitAsync(_thread);

            return _stopTask;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(IGameLoopWorkItem finalWorkItem)
    {
        ArgumentNullException.ThrowIfNull(finalWorkItem);
        RejectLoopThreadWait();

        lock (_gate)
        {
            if (ReferenceEquals(_finalWorkItem, finalWorkItem))
            {
                return _finalStopTask!;
            }

            if (_stopTask is not null || _state is not (GameLoopState.Starting or GameLoopState.Running))
            {
                return CompleteFinalStopAsync(StopAsync(), false);
            }

            _finalWorkItem = finalWorkItem;
            _finalStopTask = CompleteFinalStopAsync(StopAsync(), true);

            return _finalStopTask;
        }
    }

    /// <inheritdoc />
    public Task StopWithFinalWorkAsync(
        Func<Func<IGameLoopWorkItem, Task>, CancellationToken, Task> finalWorkAsync,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(finalWorkAsync);
        RejectLoopThreadWait();
        cancellationToken.ThrowIfCancellationRequested();
        GameLoopFinalWorkSession session;
        Task stopping;

        lock (_gate)
        {
            if (_stopTask is not null || _state is not (GameLoopState.Starting or GameLoopState.Running))
            {
                return CompleteFinalStopAsync(StopAsync(), false);
            }

            session = _finalSession = new(() => _wake.Set(), cancellationToken);
            stopping = StopAsync();
        }

        return CompleteSequenceAsync();

        async Task CompleteSequenceAsync()
        {
            Exception? failure = null;

            try
            {
                await session.Ready.Task.ConfigureAwait(false);
                _insideFinalWork.Value = true;
                await finalWorkAsync(session.DispatchAsync, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                _insideFinalWork.Value = false;

                try
                {
                    await session.CloseAsync(failure).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }

                await stopping.ConfigureAwait(false);
            }

            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            await Completion.ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public bool TryPost(IGameLoopWorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        lock (_gate)
        {
            if (_state != GameLoopState.Running ||
                !_inbox.Writer.TryWrite(new(workItem, _timeProvider.GetTimestamp())))
            {
                _rejectedWorkItems++;

                return false;
            }

            _acceptedWorkItems++;

            // Admission and signalling share the disposal lock: an accepted post cannot
            // subsequently fail because another caller disposed the wake handle.
            _wake.Set();

            return true;
        }
    }

    private async Task CompleteFinalStopAsync(Task stopping, bool captureAccepted)
    {
        await stopping.ConfigureAwait(false);
        await Completion.ConfigureAwait(false);

        if (!captureAccepted)
        {
            throw new InvalidOperationException("The game loop already stopped without this terminal work item.");
        }
    }

    private void EnsureRunning()
    {
        if (_state != GameLoopState.Running)
        {
            _rejectedWorkItems++;

            throw new InvalidOperationException("The game loop is not accepting work.");
        }
    }

    private void RejectLoopThreadWait()
    {
        if (IsOnLoopThread || _insideFinalWork.Value)
        {
            throw new InvalidOperationException("This operation cannot wait on the game loop's own thread.");
        }
    }

    private void Run()
    {
        Exception? failure = null;

        try
        {
            lock (_gate)
            {
                Volatile.Write(ref _loopThreadId, Environment.CurrentManagedThreadId);

                if (_state == GameLoopState.Starting)
                {
                    _timers.BindToCurrentThread(Wake);
                    _state = GameLoopState.Running;
                }

                _started.TrySetResult();
            }

            while (true)
            {
                var attempted = _pump.RunBatch();
                bool runTimers;

                lock (_gate)
                {
                    if (_state == GameLoopState.Stopping && !_inbox.Reader.TryPeek(out _))
                    {
                        break;
                    }

                    runTimers = _state == GameLoopState.Running;
                }

                if (runTimers)
                {
                    _timers.ProcessDueTimers();
                }

                if (attempted == 0)
                {
                    // Timer mutations and command admission use this same persistent wake signal.
                    // Ceil the wait: truncating a sub-millisecond remainder would busy-spin.
                    var delay = _timers.GetNextDelay();
                    var waitMilliseconds = delay is null
                        ? Timeout.Infinite
                        : (int)Math.Min(int.MaxValue, Math.Ceiling(delay.Value.TotalMilliseconds));
                    _wake.WaitOne(waitMilliseconds);
                }
            }

            // Admission and timers are closed, and every accepted command has finished.
            // Keep the loop identity until this final synchronous capture completes.
            _finalWorkItem?.Execute();

            if (_finalSession is not null)
            {
                _finalSession.Ready.TrySetResult();

                while (_finalSession.ExecutePending())
                {
                    _wake.WaitOne();
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception;
            _finalSession?.Fail(exception);

            lock (_gate)
            {
                _faults++;
                _timers.Close();
                _state = GameLoopState.Stopping;
                _inbox.Writer.TryComplete();
            }

            var abandoned = 0;

            while (_inbox.Reader.TryRead(out _))
            {
                abandoned++;
            }

            try
            {
                _logger.Error(exception, "Game loop failed; abandoned {AbandonedWorkItems} queued work items", abandoned);
            }
            catch (Exception)
            {
                // A failing diagnostic sink must not replace the handler failure or prevent cleanup.
            }
        }
        finally
        {
            lock (_gate)
            {
                _timers.Close();
                Volatile.Write(ref _loopThreadId, 0);
                _state = GameLoopState.Stopped;
            }

            if (failure is null)
            {
                _completion.TrySetResult();
            }
            else
            {
                _started.TrySetException(failure);
                _completion.TrySetException(failure);
            }
        }
    }

    private async ValueTask WaitForAdmissionAsync(IGameLoopWorkItem workItem, CancellationToken cancellationToken)
    {
        while (await _inbox.Writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false))
        {
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureRunning();

                if (_inbox.Writer.TryWrite(new(workItem, _timeProvider.GetTimestamp())))
                {
                    _acceptedWorkItems++;
                    _wake.Set();

                    return;
                }
            }
        }

        lock (_gate)
        {
            _rejectedWorkItems++;
        }

        throw new InvalidOperationException("The game loop is not accepting work.");
    }

    private async Task WaitForThreadExitAsync(Thread? thread)
    {
        // The host observes the primary failure through Completion. Cleanup does not report it a second time.
        await Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        // Completion is signalled in the worker's finally; join also covers the last instructions after that signal.
        thread?.Join();
    }

    private void Wake()
    {
        lock (_gate)
        {
            // A timer producer may hold a copied signal delegate while Stop/Dispose closes the loop.
            if (!_disposed && _state is GameLoopState.Starting or GameLoopState.Running)
            {
                _wake.Set();
            }
        }
    }

    /// <summary>
    ///     Drains the loop and releases its wake handle. Calls on the loop thread are rejected.
    /// </summary>
    public void Dispose()
    {
        RejectLoopThreadWait();
        StopAsync().GetAwaiter().GetResult();

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _wake.Dispose();
            _disposed = true;
        }
    }
}
