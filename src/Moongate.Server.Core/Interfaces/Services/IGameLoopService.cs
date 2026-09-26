using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>
///     Owns the dedicated, synchronous game loop and its bounded work inbox.
/// </summary>
public interface IGameLoopService : IMoongateStartupService
{
    /// <summary>
    ///     Whether the caller is currently executing on the loop thread.
    /// </summary>
    bool IsOnLoopThread { get; }

    /// <summary>
    ///     Gets the stable lifetime task, faulted with the original handler failure or completed after shutdown.
    /// </summary>
    /// <remarks>
    ///     StopAsync performs cleanup successfully after a fault; observe this task to detect loop failure.
    /// </remarks>
    Task Completion { get; }

    /// <summary>
    ///     Returns queue depth/age, admission counters, command timings and fatal loop failures.
    /// </summary>
    /// <remarks>
    ///     Concurrent execution can advance between queue and execution measurements; durations use monotonic time.
    /// </remarks>
    GameLoopMetricsSnapshot GetMetricsSnapshot();

    /// <summary>
    ///     Waits for inbox space and completes on acceptance, not execution.
    /// </summary>
    /// <remarks>
    ///     Cancellation before acceptance rejects the item; later cancellation does not retract it.
    ///     Producers must await each call to bound their pending work; the inbox does not bound unawaited producers.
    ///     Calls from the loop thread are rejected to prevent waiting for the loop's own inbox.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    ///     The work item is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     The loop is unavailable or the caller is on the loop thread.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     Cancellation was requested before acceptance.
    /// </exception>
    ValueTask PostAsync(IGameLoopWorkItem workItem, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Closes admission and timers, drains accepted work, then executes one final item on the loop thread.
    /// </summary>
    /// <remarks>
    ///     Unlike ordinary cleanup, this overload propagates loop and final-item failures. A prior ordinary stop
    ///     or a different terminal item rejects this capture explicitly. Reusing the accepted item is idempotent.
    ///     The final item must perform synchronous in-memory work only; await disk work after this task completes.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    ///     The final work item is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     The caller is on the loop thread or shutdown already won admission.
    /// </exception>
    Task StopAsync(IGameLoopWorkItem finalWorkItem);

    /// <summary>
    ///     Closes ordinary admission and timers, drains accepted work, and runs one asynchronous terminal operation off-loop.
    /// </summary>
    /// <remarks>
    ///     The operation may dispatch sequential synchronous final captures to the owner thread. The dispatcher is
    ///     valid only inside this callback; concurrent or escaped dispatch rejects. Await every capture and database operation.
    ///     Callback cancellation/failure still drains captures and joins the loop. Cancellation cannot preempt a synchronous
    ///     capture.
    ///     This method cannot be called from the loop or reentered from its terminal callback.
    /// </remarks>
    /// <param name="finalWorkAsync">
    ///     Off-loop terminal operation receiving the bounded owner-capture dispatcher and cancellation
    ///     token.
    /// </param>
    /// <param name="cancellationToken">
    ///     Cooperative cancellation for terminal work; cleanup always completes.
    /// </param>
    Task StopWithFinalWorkAsync(
        Func<Func<IGameLoopWorkItem, Task>, CancellationToken, Task> finalWorkAsync,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Attempts immediate admission; returns false when full or not running, without executing inline.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    ///     The work item is null.
    /// </exception>
    bool TryPost(IGameLoopWorkItem workItem);
}
