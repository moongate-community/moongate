namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Generates world content units on demand.
/// </summary>
public interface IWorldGenerator
{
    /// <summary>
    /// Unique generator name used for targeted execution (e.g. "doors").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Generates content for this generator instance.
    /// </summary>
    /// <param name="logCallback">Optional progress log callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task GenerateAsync(Action<string>? logCallback = null, CancellationToken cancellationToken = default);
}
