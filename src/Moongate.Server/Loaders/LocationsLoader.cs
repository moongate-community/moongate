using Moongate.Server.Interfaces;
using Moongate.UO.Data.Locations;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads the per-facet travel/location trees into <see cref="ILocationService" /> at startup: seeds
/// each embedded <c>locations/&lt;facet&gt;.yaml</c> into the data directory if missing, then parses
/// and registers each facet root.
/// </summary>
public sealed class LocationsLoader : IDataLoader
{
    private static readonly string[] Facets = ["felucca", "trammel", "ilshenar", "malas", "tokuno", "termur"];

    private readonly ILogger _logger = Log.ForContext<LocationsLoader>();
    private readonly ILocationService _locations;
    private readonly DirectoriesConfig _directories;

    public LocationsLoader(ILocationService locations, DirectoriesConfig directories)
    {
        _locations = locations;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var locationsDirectory = Path.Combine(_directories.RegisterDirectory("data"), "locations");
        Directory.CreateDirectory(locationsDirectory);

        foreach (var facet in Facets)
        {
            var path = Path.Combine(locationsDirectory, facet + ".yaml");

            if (!File.Exists(path))
            {
                var seed = ResourceUtils.GetEmbeddedResourceString(
                    typeof(LocationsLoader).Assembly,
                    $"Assets/locations/{facet}.yaml"
                );
                File.WriteAllText(path, seed);
                _logger.Information("Seeded default {Facet}.yaml at {Path}", facet, path);
            }

            var facetRoot = YamlUtils.DeserializeFromFile<LocationCategory>(path);
            _locations.Register(facetRoot);
        }

        _logger.Information("Loaded {Count} location facet(s)", Facets.Length);

        return ValueTask.CompletedTask;
    }
}
