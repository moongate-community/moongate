using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

public class StartingCitiesLoader : IDataLoader<StartingCityContent>
{
    private readonly ILogger _logger = Log.ForContext<StartingCitiesLoader>();

    private string startingCitiesFilePath => Path.Join(_directoriesConfig["data"], "starting_cities.toml");

    private readonly DirectoriesConfig _directoriesConfig;

    public StartingCitiesLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(startingCitiesFilePath))
        {
            throw new FileNotFoundException("Starting cities file starting_cities.toml not found");
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<StartingCityContent>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var startingCitiesFile =
            await TomlUtils.DeserializeFromFileAsync<StartingCityFile>(startingCitiesFilePath, null, cancellationToken);

        _logger.Information("Found {Count} starting cities", startingCitiesFile?.StartingCity.Count);

        return new DataLoaderResult<StartingCityContent>()
        {
            Entities = startingCitiesFile.StartingCity
        };
    }
}
