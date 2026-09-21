namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>A service the host starts and stops as part of its own lifecycle.</summary>
/// <remarks>
/// The bootstrap starts registered services in ascending registration priority and stops them in
/// reverse, so a service takes a lower priority than the services that depend on it. A service whose
/// start failed is still asked to stop, so <see cref="StopAsync" /> must tolerate a partial start.
/// </remarks>
public interface IMoongateStartupService : IMoongateService
{
    /// <summary>Starts the service.</summary>
    /// <returns>A task that completes when the service is ready for use.</returns>
    /// <remarks>
    /// Throw to abort startup. The bootstrap then stops every service it already started and the
    /// host does not run.
    /// </remarks>
    Task StartAsync();

    /// <summary>Stops the service and releases what it holds.</summary>
    /// <returns>A task that completes when the service has stopped.</returns>
    /// <remarks>Called once during shutdown, whether or not <see cref="StartAsync" /> succeeded.</remarks>
    Task StopAsync();
}
