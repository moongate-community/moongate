using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Owns the dedicated, synchronous game loop and its bounded work inbox.</summary>
public interface IGameLoopService : IMoongateStartupService
{
    /// <summary>Whether the caller is currently executing on the loop thread.</summary>
    bool IsOnLoopThread { get; }

    /// <summary>Gets the stable lifetime task, faulted with the original handler failure or completed after shutdown.</summary>
    /// <remarks>StopAsync performs cleanup successfully after a fault; observe this task to detect loop failure.</remarks>
    Task Completion { get; }

    /// <summary>Attempts immediate admission; returns false when full or not running, without executing inline.</summary>
    /// <exception cref="ArgumentNullException">The work item is null.</exception>
    bool TryPost(IGameLoopWorkItem workItem);

    /// <summary>Waits for inbox space and completes on acceptance, not execution.</summary>
    /// <remarks>
    /// Cancellation before acceptance rejects the item; later cancellation does not retract it.
    /// Producers must await each call to bound their pending work; the inbox does not bound unawaited producers.
    /// Calls from the loop thread are rejected to prevent waiting for the loop's own inbox.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The work item is null.</exception>
    /// <exception cref="InvalidOperationException">The loop is unavailable or the caller is on the loop thread.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested before acceptance.</exception>
    ValueTask PostAsync(IGameLoopWorkItem workItem, CancellationToken cancellationToken = default);
}
