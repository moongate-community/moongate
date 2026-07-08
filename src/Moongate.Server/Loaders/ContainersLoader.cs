using Moongate.Server.Interfaces;
using Moongate.UO.Data.Containers;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads container definitions into <see cref="IContainerService" /> at startup: seeds the embedded
/// <c>containers.yaml</c> into the data directory if missing, then parses and registers it.
/// </summary>
public sealed class ContainersLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<ContainersLoader>();
    private readonly IContainerService _containers;
    private readonly DirectoriesConfig _directories;

    public ContainersLoader(IContainerService containers, DirectoriesConfig directories)
    {
        _containers = containers;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "containers.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(typeof(ContainersLoader).Assembly, "Assets/containers.yaml");
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default containers.yaml at {Path}", path);
        }

        var containers = YamlUtils.DeserializeFromFile<ContainerDefinition[]>(path) ?? [];

        foreach (var container in containers)
        {
            _containers.Register(container);
        }

        _logger.Information("Loaded {Count} container definition(s) from {Path}", containers.Length, path);

        return ValueTask.CompletedTask;
    }
}
