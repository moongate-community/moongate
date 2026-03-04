using System.Collections.Concurrent;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Serilog;

namespace Moongate.Server.Services.EventLoop;

/// <summary>
/// Executes background jobs on worker threads and dispatches queued callbacks to the game loop.
/// </summary>
public sealed class BackgroundJobService : IBackgroundJobService, IDisposable
{
    private readonly ConcurrentQueue<Func<Task>> _backgroundJobs = new();
    private readonly ConcurrentQueue<Action> _gameLoopActions = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly List<Task> _workers = [];
    private readonly ILogger _logger = Log.ForContext<BackgroundJobService>();
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _running;
    private bool _stopped;

    public void Start(int? workerCount = null)
    {
        if (_running)
        {
            return;
        }

        if (_stopped)
        {
            throw new InvalidOperationException("Background job service has already been stopped.");
        }

        var resolvedWorkerCount = workerCount ?? Math.Max(1, Environment.ProcessorCount / 2);

        if (resolvedWorkerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workerCount));
        }

        _cancellationTokenSource = new();
        _running = true;

        for (var i = 0; i < resolvedWorkerCount; i++)
        {
            _workers.Add(Task.Run(() => WorkerLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token));
        }
    }

    public async Task StopAsync()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _running = false;

        if (_cancellationTokenSource is null)
        {
            return;
        }

        _cancellationTokenSource.Cancel();

        for (var i = 0; i < _workers.Count; i++)
        {
            _signal.Release();
        }

        try
        {
            await Task.WhenAll(_workers);
        }
        catch (OperationCanceledException) { }
    }

    public void EnqueueBackground(Action job)
    {
        ArgumentNullException.ThrowIfNull(job);

        EnqueueBackground(
            () =>
            {
                job();

                return Task.CompletedTask;
            }
        );
    }

    public void EnqueueBackground(Func<Task> job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (_stopped)
        {
            throw new InvalidOperationException("Background job service has already been stopped.");
        }

        _backgroundJobs.Enqueue(job);
        _signal.Release();
    }

    public void RunBackgroundAndPostResult<TResult>(
        Func<TResult> backgroundJob,
        Action<TResult> onGameLoopResult,
        Action<Exception>? onGameLoopError = null
    )
    {
        ArgumentNullException.ThrowIfNull(backgroundJob);
        ArgumentNullException.ThrowIfNull(onGameLoopResult);

        EnqueueBackground(
            () =>
            {
                try
                {
                    var result = backgroundJob();

                    PostToGameLoop(() => onGameLoopResult(result));
                }
                catch (Exception ex)
                {
                    if (onGameLoopError is not null)
                    {
                        PostToGameLoop(() => onGameLoopError(ex));
                    }
                }

                return Task.CompletedTask;
            }
        );
    }

    public void RunBackgroundAndPostResultAsync<TResult>(
        Func<Task<TResult>> backgroundJob,
        Action<TResult> onGameLoopResult,
        Action<Exception>? onGameLoopError = null
    )
    {
        ArgumentNullException.ThrowIfNull(backgroundJob);
        ArgumentNullException.ThrowIfNull(onGameLoopResult);

        EnqueueBackground(
            async () =>
            {
                try
                {
                    var result = await backgroundJob();

                    PostToGameLoop(() => onGameLoopResult(result));
                }
                catch (Exception ex)
                {
                    if (onGameLoopError is not null)
                    {
                        PostToGameLoop(() => onGameLoopError(ex));
                    }
                }
            }
        );
    }

    public void PostToGameLoop(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _gameLoopActions.Enqueue(action);
    }

    public int ExecutePendingOnGameLoop(int maxActions = 100)
    {
        if (maxActions <= 0)
        {
            return 0;
        }

        var executed = 0;

        while (executed < maxActions && _gameLoopActions.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Game loop callback failed.");
            }

            executed++;
        }

        return executed;
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _cancellationTokenSource?.Dispose();
        _signal.Dispose();
    }

    private async Task WorkerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_backgroundJobs.TryDequeue(out var job))
            {
                continue;
            }

            try
            {
                await job();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Background job execution failed.");
            }
        }
    }
}
