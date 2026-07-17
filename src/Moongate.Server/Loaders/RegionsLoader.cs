using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Regions;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads map regions into <see cref="IRegionService" /> at startup: seeds the embedded
/// <c>regions.yaml</c> into the data directory if missing, then parses and registers it.
/// </summary>
public sealed class RegionsLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<RegionsLoader>();
    private readonly IRegionService _regions;
    private readonly DirectoriesConfig _directories;

    public RegionsLoader(IRegionService regions, DirectoriesConfig directories)
    {
        _regions = regions;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "regions.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(typeof(RegionsLoader).Assembly, "Assets/regions.yaml");
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default regions.yaml at {Path}", path);
        }

        var regions = YamlUtils.DeserializeFromFile<RegionDefinition[]>(path) ?? [];

        foreach (var region in regions)
        {
            _regions.Register(region);
        }

        _logger.Information("Loaded {Count} region(s) from {Path}", regions.Length, path);

        return ValueTask.CompletedTask;
    }
}
