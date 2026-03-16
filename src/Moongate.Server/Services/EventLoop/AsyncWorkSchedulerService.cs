using System.Collections.Concurrent;
using Moongate.Server.Interfaces.Services.EvenLoop;

namespace Moongate.Server.Services.EventLoop;

/// <summary>
/// Deduplicates asynchronous work by queue/key and marshals completion back to the game loop.
/// </summary>
public sealed class AsyncWorkSchedulerService : IAsyncWorkSchedulerService
{
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly ConcurrentDictionary<WorkKey, byte> _inFlight = [];

    public AsyncWorkSchedulerService(IBackgroundJobService backgroundJobService)
    {
        _backgroundJobService = backgroundJobService ?? throw new ArgumentNullException(nameof(backgroundJobService));
    }

    public bool TrySchedule<TKey, TResult>(
        string queueName,
        TKey key,
        Func<CancellationToken, Task<TResult>> backgroundWork,
        Action<TResult> onGameLoopResult,
        Action<Exception>? onGameLoopError = null,
        TimeSpan? timeout = null
    )
        where TKey : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(backgroundWork);
        ArgumentNullException.ThrowIfNull(onGameLoopResult);

        var workKey = new WorkKey(queueName.Trim(), key);
        if (!_inFlight.TryAdd(workKey, 0))
        {
            return false;
        }

        _backgroundJobService.RunBackgroundAndPostResultAsync(
            async () =>
            {
                using var timeoutSource = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : null;
                var token = timeoutSource?.Token ?? CancellationToken.None;

                return await backgroundWork(token).ConfigureAwait(false);
            },
            result =>
            {
                try
                {
                    onGameLoopResult(result);
                }
                finally
                {
                    _inFlight.TryRemove(workKey, out _);
                }
            },
            ex =>
            {
                try
                {
                    onGameLoopError?.Invoke(ex);
                }
                finally
                {
                    _inFlight.TryRemove(workKey, out _);
                }
            }
        );

        return true;
    }

    private readonly record struct WorkKey(string QueueName, object Key);
}
