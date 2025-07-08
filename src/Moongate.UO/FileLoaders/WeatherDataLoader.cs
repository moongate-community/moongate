using Moongate.Core.Directories;
using Moongate.Core.Json;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Interfaces.FileLoaders;

namespace Moongate.UO.FileLoaders;

public class WeatherDataLoader : IFileLoader
{
    private readonly DirectoriesConfig _directoriesConfig;

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
        }
    }
}
