using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the races of <c>data/races.toml</c>. A race listed twice, a race without both genders, a body id below 1
///     or a style id outside 1 to 0xFFFF stops the server at startup.
/// </summary>
public class RacesLoader : IDataLoader<RaceContent>
{
    private const int MaxItemId = 0xFFFF;

    private readonly DirectoriesConfig _directoriesConfig;

    private readonly ILogger _logger = Log.ForContext<RacesLoader>();

    private string racesFilePath => Path.Join(_directoriesConfig["data"], "races.toml");

    public RacesLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(racesFilePath))
        {
            throw new FileNotFoundException("Races file races.toml not found", racesFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<RaceContent>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var racesFile = await TomlUtils.DeserializeFromFileAsync<RaceContentFile>(racesFilePath, null, cancellationToken);
        var races = racesFile?.Race ?? [];

        if (races.Count == 0)
        {
            throw new InvalidDataException($"{racesFilePath} has no [[race]] entries.");
        }

        var seen = new HashSet<RaceType>();

        foreach (var race in races)
        {
            if (!seen.Add(race.Race))
            {
                throw new InvalidDataException($"{racesFilePath}: race {race.Race} is listed more than once.");
            }

            Validate(race, race.Male, "male");
            Validate(race, race.Female, "female");
        }

        _logger.Information("Found {Count} races", races.Count);

        return new DataLoaderResult<RaceContent>()
        {
            Entities = races
        };
    }

    private void Validate(RaceContent race, RaceGenderContent? gender, string genderName)
    {
        if (gender is null)
        {
            throw new InvalidDataException($"{racesFilePath}: race {race.Race} has no [race.{genderName}] section.");
        }

        if (gender.Body < 1)
        {
            throw new InvalidDataException($"{racesFilePath}: race {race.Race} {genderName} has no body id.");
        }

        var invalidStyle = gender.Hair.Concat(gender.Beard)
            .Select(style => (int?)style)
            .FirstOrDefault(style => style is < 1 or > MaxItemId);

        if (invalidStyle is { } style)
        {
            throw new InvalidDataException(
                $"{racesFilePath}: race {race.Race} {genderName} lists style {style}; " +
                "styles must be item ids from 1 to 0xFFFF (no hair or beard is always allowed and is not listed)."
            );
        }
    }
}
