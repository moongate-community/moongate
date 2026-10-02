using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Moongates;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the public moongates of <c>data/moongates.toml</c>. A facet without a map or with one listed twice, a
///     facet without destinations, a missing client text or location, a spot outside the 16-bit coordinates or a hue
///     that is not one stops the server at startup.
/// </summary>
public class MoongatesLoader : IDataLoader<MoongateFacet>
{
    private readonly ILogger _logger = Log.ForContext<MoongatesLoader>();

    private readonly DirectoriesConfig _directoriesConfig;

    private string moongatesFilePath => Path.Join(_directoriesConfig["data"], "moongates.toml");

    public MoongatesLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(moongatesFilePath))
        {
            throw new FileNotFoundException("Moongates file moongates.toml not found", moongatesFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<MoongateFacet>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var file = await TomlUtils.DeserializeFromFileAsync<MoongateFile>(moongatesFilePath, null, cancellationToken);
        var facets = file?.Facet ?? [];
        var maps = new HashSet<MapType>();

        foreach (var facet in facets)
        {
            if (!Enum.IsDefined(facet.Map))
            {
                throw new InvalidDataException($"{moongatesFilePath}: a [[facet]] has no map.");
            }

            if (!maps.Add(facet.Map))
            {
                throw new InvalidDataException($"{moongatesFilePath}: the map {facet.Map} has two [[facet]] entries.");
            }

            if (facet.Cliloc <= 0 || facet.SelectedCliloc <= 0)
            {
                throw new InvalidDataException($"{moongatesFilePath}: the facet {facet.Map} needs cliloc and selected_cliloc.");
            }

            if (facet.Destination.Count == 0)
            {
                throw new InvalidDataException($"{moongatesFilePath}: the facet {facet.Map} has no [[facet.destination]].");
            }

            foreach (var destination in facet.Destination)
            {
                Check(facet, destination);
            }
        }

        _logger.Information(
            "Found {Count} moongates on {Facets} maps",
            facets.Sum(facet => facet.Destination.Count),
            facets.Count
        );

        return new DataLoaderResult<MoongateFacet> { Entities = facets };
    }

    private void Check(MoongateFacet facet, MoongateDestination destination)
    {
        var location = destination.Location;

        // No gate stands on the corner of the map: a location of zeros is one left out.
        if (string.IsNullOrWhiteSpace(destination.Name) ||
            destination.Cliloc <= 0 ||
            location == default ||
            location.X is < 0 or > ushort.MaxValue ||
            location.Y is < 0 or > ushort.MaxValue ||
            location.Z is < sbyte.MinValue or > sbyte.MaxValue ||
            destination.Hue is < 0 or > ushort.MaxValue)
        {
            throw new InvalidDataException(
                $"{moongatesFilePath}: destination '{destination.Name}' of {facet.Map} needs a name, a cliloc, a location inside the map coordinates and a hue from 0 to 65535."
            );
        }
    }
}
