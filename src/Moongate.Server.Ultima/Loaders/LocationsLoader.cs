using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Locations;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the named places of <c>data/locations.toml</c>. The file may be missing: there are then no places. A
///     place without a map, a name or a location, a spot outside the 16-bit coordinates or a category with an empty
///     part stops the server at startup.
/// </summary>
public class LocationsLoader : IDataLoader<NamedLocation>
{
    private readonly ILogger _logger = Log.ForContext<LocationsLoader>();

    private readonly DirectoriesConfig _directoriesConfig;

    private string locationsFilePath => Path.Join(_directoriesConfig["data"], "locations.toml");

    public LocationsLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<NamedLocation>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(locationsFilePath))
        {
            _logger.Information("No locations.toml: go has no named places");

            return new DataLoaderResult<NamedLocation> { Entities = [] };
        }

        var file = await TomlUtils.DeserializeFromFileAsync<LocationFile>(locationsFilePath, null, cancellationToken);
        var places = file?.Location ?? [];

        foreach (var place in places)
        {
            Check(place);
        }

        _logger.Information("Found {Count} named places", places.Count);

        return new DataLoaderResult<NamedLocation> { Entities = places };
    }

    private void Check(NamedLocation place)
    {
        if (string.IsNullOrWhiteSpace(place.Name))
        {
            throw new InvalidDataException($"{locationsFilePath}: a [[location]] has no name.");
        }

        if (!Enum.IsDefined(place.Map))
        {
            throw new InvalidDataException($"{locationsFilePath}: the place '{place.Name}' has no map.");
        }

        var location = place.Location;

        // No place is the corner of the map: a location of zeros is one left out.
        if (location == default ||
            location.X is < 0 or > ushort.MaxValue ||
            location.Y is < 0 or > ushort.MaxValue ||
            location.Z is < sbyte.MinValue or > sbyte.MaxValue)
        {
            throw new InvalidDataException(
                $"{locationsFilePath}: the place '{place.Name}' of {place.Map} needs a location inside the map coordinates."
            );
        }

        place.Category ??= "";

        if (place.Category.Length > 0 && place.Category.Split('/').Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException(
                $"{locationsFilePath}: the category '{place.Category}' of the place '{place.Name}' has an empty part."
            );
        }
    }
}
