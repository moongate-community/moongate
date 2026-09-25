namespace Moongate.Server.Core.Interfaces.Bootstrap;

/// <summary>
///     Drives the host through startup, its running phase, and shutdown.
/// </summary>
/// <remarks>
///     Each phase runs once: concurrent or repeated calls to the same method share the task of the call
///     already in flight rather than running the phase again. Lifecycle events are published at the
///     boundaries of each phase, so plugins and services can observe them.
/// </remarks>
public interface IMoongateServerBootstrap
{
    /// <summary>
    ///     Waits until the host is asked to shut down or the game loop ends on its own.
    /// </summary>
    /// <returns>
    ///     A task that completes on cancellation, or faults with the game loop's failure.
    /// </returns>
    /// <remarks>
    ///     This method does not start or stop anything. It blocks the running phase between
    ///     <see cref="StartAsync" /> and <see cref="StopAsync" />, and a game loop that faulted surfaces its
    ///     exception here so the host can report it before shutting down.
    /// </remarks>
    Task RunAsync();

    /// <summary>
    ///     Starts every registered startup service in priority order.
    /// </summary>
    /// <returns>
    ///     A task that completes when every service has started.
    /// </returns>
    /// <remarks>
    ///     A service that throws aborts startup: the services started so far are stopped in reverse order,
    ///     the container is disposed, and the failure propagates to the caller.
    /// </remarks>
    Task StartAsync();

    /// <summary>
    ///     Stops the started services in reverse order and disposes the container.
    /// </summary>
    /// <returns>
    ///     A task that completes when shutdown has finished.
    /// </returns>
    /// <remarks>
    ///     Safe to call after a failed start, in which case it only cleans up. Failures from individual
    ///     services are collected so every service still gets its stop call.
    /// </remarks>
    Task StopAsync();
}
