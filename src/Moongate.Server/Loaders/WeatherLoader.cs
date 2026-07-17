using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Weather;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads weather profiles into <see cref="IWeatherService" /> at startup: seeds the embedded
/// <c>weather.yaml</c> into the data directory if missing, then parses and registers it.
/// </summary>
public sealed class WeatherLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<WeatherLoader>();
    private readonly IWeatherService _weather;
    private readonly DirectoriesConfig _directories;

    public WeatherLoader(IWeatherService weather, DirectoriesConfig directories)
    {
        _weather = weather;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "weather.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(typeof(WeatherLoader).Assembly, "Assets/weather.yaml");
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default weather.yaml at {Path}", path);
        }

        var weatherTypes = YamlUtils.DeserializeFromFile<WeatherType[]>(path) ?? [];

        foreach (var weather in weatherTypes)
        {
            _weather.Register(weather);
        }

        _logger.Information("Loaded {Count} weather type(s) from {Path}", weatherTypes.Length, path);

        return ValueTask.CompletedTask;
    }
}
