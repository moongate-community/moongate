using Moongate.Server.Bootstrap;

namespace Moongate.Server.Interfaces.Bootstrap;

/// <summary>
/// Represents a discrete phase of the server bootstrap pipeline.
/// Phases are executed in <see cref="Order"/> sequence during initialization.
/// </summary>
public interface IBootstrapPhase
{
    /// <summary>
    /// Gets the execution order for this phase. Lower values run first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Gets the display name for this phase (used in logging).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Configures this phase using the shared bootstrap context.
    /// </summary>
    void Configure(BootstrapContext context);
}
