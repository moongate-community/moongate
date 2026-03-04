namespace Moongate.Server.Interfaces.Services.EvenLoop;

/// <summary>
/// Schedules background jobs and marshals callbacks back to the game loop thread.
/// </summary>
public interface IBackgroundJobService
{
    /// <summary>
    /// Starts the background workers.
    /// </summary>
    /// <param name="workerCount">Optional number of workers. Uses default when null.</param>
    void Start(int? workerCount = null);

    /// <summary>
    /// Stops all workers and waits for worker termination.
    /// </summary>
    /// <returns>Completion task.</returns>
    Task StopAsync();

    /// <summary>
    /// Enqueues a synchronous background action.
    /// </summary>
    /// <param name="job">Action to execute in a worker.</param>
    void EnqueueBackground(Action job);

    /// <summary>
    /// Enqueues an asynchronous background action.
    /// </summary>
    /// <param name="job">Task-producing action to execute in a worker.</param>
    void EnqueueBackground(Func<Task> job);

    /// <summary>
    /// Runs a background job and posts its result callback to game loop.
    /// </summary>
    /// <typeparam name="TResult">Background result type.</typeparam>
    /// <param name="backgroundJob">Background job function.</param>
    /// <param name="onGameLoopResult">Callback executed on game loop with result.</param>
    /// <param name="onGameLoopError">Optional callback executed on game loop on failure.</param>
    void RunBackgroundAndPostResult<TResult>(
        Func<TResult> backgroundJob,
        Action<TResult> onGameLoopResult,
        Action<Exception>? onGameLoopError = null
    );

    /// <summary>
    /// Runs an async background job and posts its result callback to game loop.
    /// </summary>
    /// <typeparam name="TResult">Background result type.</typeparam>
    /// <param name="backgroundJob">Async background job function.</param>
    /// <param name="onGameLoopResult">Callback executed on game loop with result.</param>
    /// <param name="onGameLoopError">Optional callback executed on game loop on failure.</param>
    void RunBackgroundAndPostResultAsync<TResult>(
        Func<Task<TResult>> backgroundJob,
        Action<TResult> onGameLoopResult,
        Action<Exception>? onGameLoopError = null
    );

    /// <summary>
    /// Enqueues an action to be executed on the game loop thread.
    /// </summary>
    /// <param name="action">Action to execute in the game loop phase.</param>
    void PostToGameLoop(Action action);

    /// <summary>
    /// Executes pending game-loop callbacks up to the provided limit.
    /// </summary>
    /// <param name="maxActions">Maximum callbacks to execute.</param>
    /// <returns>Executed callbacks count.</returns>
    int ExecutePendingOnGameLoop(int maxActions = 100);
}
