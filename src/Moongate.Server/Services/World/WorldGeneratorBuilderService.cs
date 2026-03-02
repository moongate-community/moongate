using Moongate.Server.Interfaces.Services.World;

namespace Moongate.Server.Services.World;

/// <summary>
/// Executes all registered world generators when explicitly invoked.
/// </summary>
public sealed class WorldGeneratorBuilderService : IWorldGeneratorBuilderService
{
    private readonly IReadOnlyList<IWorldGenerator> _worldGenerators;

    public WorldGeneratorBuilderService(IEnumerable<IWorldGenerator> worldGenerators)
    {
        _worldGenerators = worldGenerators.ToArray();
    }

    /// <inheritdoc />
    public async Task GenerateAsync(
        string? generatorName = null,
        Action<string>? logCallback = null,
        CancellationToken cancellationToken = default
    )
    {
        var selectedGenerators = ResolveGenerators(generatorName);

        foreach (var worldGenerator in selectedGenerators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logCallback?.Invoke($"Starting world generator '{worldGenerator.Name}'.");
            await worldGenerator.GenerateAsync(logCallback, cancellationToken);
            logCallback?.Invoke($"Completed world generator '{worldGenerator.Name}'.");
        }
    }

    private IReadOnlyList<IWorldGenerator> ResolveGenerators(string? generatorName)
    {
        if (string.IsNullOrWhiteSpace(generatorName))
        {
            return _worldGenerators;
        }

        var selectedGenerator = _worldGenerators.FirstOrDefault(
            generator => string.Equals(generator.Name, generatorName, StringComparison.OrdinalIgnoreCase)
        );

        if (selectedGenerator is null)
        {
            throw new InvalidOperationException($"World generator '{generatorName}' was not found.");
        }

        return [selectedGenerator];
    }
}
