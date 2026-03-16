namespace Moongate.Server.Interfaces.Services.EvenLoop;

/// <summary>
/// Schedules asynchronous work off the game loop and posts the result back to the game loop.
/// </summary>
public interface IAsyncWorkSchedulerService
{
    /// <summary>
    /// Attempts to schedule a unit of asynchronous work if no work is currently in-flight for the same queue/key.
    /// </summary>
    bool TrySchedule<TKey, TResult>(
        string queueName,
        TKey key,
        Func<CancellationToken, Task<TResult>> backgroundWork,
        Action<TResult> onGameLoopResult,
        Action<Exception>? onGameLoopError = null,
        TimeSpan? timeout = null
    )
        where TKey : notnull;
}
