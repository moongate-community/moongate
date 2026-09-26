using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Weather;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the weather profiles of <c>data/weather.toml</c>. A profile without a name, a name used twice, a chance
///     outside 0 to 100 or a lowest temperature above the highest stops the server at startup.
/// </summary>
public class WeatherLoader : IDataLoader<WeatherContent>
{
    private readonly DirectoriesConfig _directoriesConfig;

    private readonly ILogger _logger = Log.ForContext<WeatherLoader>();

    private string weatherFilePath => Path.Join(_directoriesConfig["data"], "weather.toml");

    public WeatherLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(weatherFilePath))
        {
            throw new FileNotFoundException("Weather file weather.toml not found", weatherFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<WeatherContent>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var weatherFile = await TomlUtils.DeserializeFromFileAsync<WeatherContentFile>(weatherFilePath, null, cancellationToken);
        var profiles = weatherFile?.Weather ?? [];

        if (profiles.Count == 0)
        {
            throw new InvalidDataException($"{weatherFilePath} has no [[weather]] entries.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var profile in profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Name) || !names.Add(profile.Name))
            {
                throw new InvalidDataException(
                    $"{weatherFilePath}: every profile needs a name used once, found '{profile.Name}'."
                );
            }

            int[] chances = [profile.RainChance, profile.SnowChance, profile.StormChance, profile.ColdChance, profile.HeatChance];

            if (chances.Any(chance => chance is < 0 or > 100) || profile.MinTemperature > profile.MaxTemperature)
            {
                throw new InvalidDataException(
                    $"{weatherFilePath}: profile '{profile.Name}' needs chances from 0 to 100 and a lowest temperature not above the highest."
                );
            }
        }

        _logger.Information("Found {Count} weather profiles", profiles.Count);

        return new DataLoaderResult<WeatherContent>()
        {
            Entities = profiles
        };
    }
}
