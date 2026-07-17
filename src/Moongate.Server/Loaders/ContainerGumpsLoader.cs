using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.UO.Data.Containers;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads container gump layouts into <see cref="IContainerGumpService" /> at startup: seeds the
/// embedded <c>container_gumps.yaml</c> into the data directory if missing, then parses and registers it.
/// </summary>
public sealed class ContainerGumpsLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<ContainerGumpsLoader>();
    private readonly IContainerGumpService _gumps;
    private readonly DirectoriesConfig _directories;

    public ContainerGumpsLoader(IContainerGumpService gumps, DirectoriesConfig directories)
    {
        _gumps = gumps;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "container_gumps.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(
                typeof(ContainerGumpsLoader).Assembly,
                "Assets/container_gumps.yaml"
            );
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default container_gumps.yaml at {Path}", path);
        }

        var layouts = YamlUtils.DeserializeFromFile<ContainerGumpLayout[]>(path) ?? [];

        foreach (var layout in layouts)
        {
            _gumps.Register(layout);
        }

        _logger.Information("Loaded {Count} container gump layout(s) from {Path}", layouts.Length, path);

        return ValueTask.CompletedTask;
    }
}
