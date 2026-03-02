namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Orchestrates execution of all registered world generators.
/// </summary>
public interface IWorldGeneratorBuilderService
{
    /// <summary>
    /// Executes each registered world generator in registration order, or a single named generator when provided.
    /// </summary>
    /// <param name="generatorName">Optional generator name. Null/empty runs all generators.</param>
    /// <param name="logCallback">Optional progress log callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task GenerateAsync(
        string? generatorName = null,
        Action<string>? logCallback = null,
        CancellationToken cancellationToken = default
    );
}
