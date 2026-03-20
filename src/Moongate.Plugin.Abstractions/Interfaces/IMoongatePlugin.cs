namespace Moongate.Plugin.Abstractions.Interfaces;

/// <summary>
/// Defines the entry point contract for a Moongate plugin.
/// </summary>
public interface IMoongatePlugin
{
    /// <summary>
    /// Gets the unique plugin id.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the plugin display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the plugin version.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the plugin authors.
    /// </summary>
    IReadOnlyList<string> Authors { get; }

    /// <summary>
    /// Gets the optional plugin description.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Registers plugin contributions before final server bootstrap wiring.
    /// </summary>
    /// <param name="context">The plugin registration context.</param>
    void Configure(IMoongatePluginContext context);

    /// <summary>
    /// Initializes the plugin after the runtime is ready.
    /// </summary>
    /// <param name="context">The runtime plugin context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when initialization finishes.</returns>
    Task InitializeAsync(
        IMoongatePluginRuntimeContext context,
        CancellationToken cancellationToken
    );
}
