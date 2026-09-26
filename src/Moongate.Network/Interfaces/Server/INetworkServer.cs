namespace Moongate.Network.Interfaces.Server;

/// <summary>
///     Represents a network listener with common lifecycle and metadata.
/// </summary>
public interface INetworkServer : IAsyncDisposable
{
    /// <summary>
    ///     Current listening port. Returns 0 when no concrete port is bound.
    /// </summary>
    int Port { get; }

    /// <summary>
    ///     Indicates whether the server is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    ///     Starts the listener after any previous generation has stopped. Repeated starts share startup.
    /// </summary>
    /// <param name="cancellationToken">
    ///     The first successful start token controls the listener generation.
    /// </param>
    /// <returns>
    ///     A task that completes when the listener has started.
    /// </returns>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Closes the listener and waits for every owned connection to finish cleanup.
    /// </summary>
    /// <param name="cancellationToken">
    ///     Cancels only this wait; cleanup continues and can be awaited again.
    /// </param>
    /// <returns>
    ///     A task that completes when the listener has stopped.
    /// </returns>
    Task StopAsync(CancellationToken cancellationToken);
}
