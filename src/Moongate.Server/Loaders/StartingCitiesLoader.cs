using Moongate.Server.Interfaces.Loading;
using Moongate.Server.Interfaces.World;
using Moongate.UO.Data.StartingCities;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads starting cities into <see cref="IStartingCityService" /> at startup: seeds the embedded
/// <c>starting_cities.yaml</c> into the data directory if missing, then parses and registers it in order.
/// </summary>
public sealed class StartingCitiesLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<StartingCitiesLoader>();
    private readonly IStartingCityService _cities;
    private readonly DirectoriesConfig _directories;

    public StartingCitiesLoader(IStartingCityService cities, DirectoriesConfig directories)
    {
        _cities = cities;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "starting_cities.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(
                typeof(StartingCitiesLoader).Assembly,
                "Assets/starting_cities.yaml"
            );
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default starting_cities.yaml at {Path}", path);
        }

        var cities = YamlUtils.DeserializeFromFile<StartingCity[]>(path) ?? [];

        foreach (var city in cities)
        {
            _cities.Register(city);
        }

        _logger.Information("Loaded {Count} starting cit(y/ies) from {Path}", cities.Length, path);

        return ValueTask.CompletedTask;
    }
}
