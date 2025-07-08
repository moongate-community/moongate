using Moongate.Core.Directories;
using Moongate.Core.Json;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Interfaces.FileLoaders;
using Serilog;

namespace Moongate.UO.FileLoaders;

public class WeatherDataLoader : IFileLoader
{
    private readonly DirectoriesConfig _directoriesConfig;

    private readonly ILogger _logger = Log.ForContext<WeatherDataLoader>();


    public WeatherDataLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public async Task LoadAsync()
    {
        var weatherDataDirectory = Path.Combine(_directoriesConfig[DirectoryType.Data], "weather");

        var weatherTypes = Directory.GetFiles(weatherDataDirectory, "*.json");

        foreach (var weatherFile in weatherTypes)
        {
            var weatherData = JsonUtils.DeserializeFromFile<JsonWeatherWrap>(weatherFile);
            _logger.Information(
                "Loaded {WeatherType} weather from file: {FilePath}",
                weatherData.WeatherTypes.Count,
                weatherFile
            );
        }
    }
}
