using System.Text;
using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Packets.Characters;
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

        var cities = startingCitiesFile?.StartingCity ?? [];

        // The character list (0xA9) carries these cities, so check its limits here instead of at game login.
        if (cities.Count is 0 or > CharacterListPacket.MaximumCityCount)
        {
            throw new InvalidDataException(
                $"{startingCitiesFilePath} needs from 1 to {CharacterListPacket.MaximumCityCount} [[starting_city]] entries, found {cities.Count}."
            );
        }

        foreach (var city in cities)
        {
            if (!IsCityText(city.Town) || !IsCityText(city.Description))
            {
                throw new InvalidDataException(
                    $"{startingCitiesFilePath}: city '{city.Town}' needs a town and a description of 1 to {CharacterListPacket.CityTextLength} ASCII characters."
                );
            }
        }

        _logger.Information("Found {Count} starting cities", cities.Count);

        return new DataLoaderResult<StartingCityContent>()
        {
            Entities = cities
        };
    }

    private static bool IsCityText(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               text.Length <= CharacterListPacket.CityTextLength &&
               Ascii.IsValid(text);
    }
}
