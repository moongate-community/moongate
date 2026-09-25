using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the regions of every <c>data/regions/&lt;map&gt;.toml</c> file, where the file name is the map
///     (<c>felucca.toml</c>). A file named after no map, a region without areas, an area whose end is not past its
///     start, a name used twice on a map or a parent that is missing or loops back stops the server at startup.
/// </summary>
public class RegionsLoader : IDataLoader<RegionContent>
{
    private readonly DirectoriesConfig _directoriesConfig;

    private readonly ILogger _logger = Log.ForContext<RegionsLoader>();

    private string regionsDirectory => Path.Join(_directoriesConfig["data"], "regions");

    public RegionsLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(regionsDirectory))
        {
            throw new DirectoryNotFoundException($"Regions directory {regionsDirectory} not found");
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<RegionContent>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var regions = new List<RegionContent>();

        foreach (var file in Directory.GetFiles(regionsDirectory, "*.toml").Order(StringComparer.Ordinal))
        {
            if (!Enum.TryParse<MapType>(Path.GetFileNameWithoutExtension(file), true, out var map))
            {
                throw new InvalidDataException($"{file}: the file name must be a map, such as felucca.toml.");
            }

            var regionFile = await TomlUtils.DeserializeFromFileAsync<RegionContentFile>(file, null, cancellationToken);
            var mapRegions = regionFile?.Region ?? [];

            foreach (var region in mapRegions)
            {
                region.Map = map;
                ValidateAreas(file, region);
            }

            ValidateNamesAndParents(file, mapRegions);
            regions.AddRange(mapRegions);
        }

        _logger.Information("Found {Count} regions", regions.Count);

        return new DataLoaderResult<RegionContent>()
        {
            Entities = regions
        };
    }

    private static void ValidateAreas(string file, RegionContent region)
    {
        if (region.Areas.Count == 0)
        {
            throw new InvalidDataException($"{file}: region '{region.Name}' has no areas.");
        }

        foreach (var area in region.Areas)
        {
            var valid = area.X1 < area.X2 &&
                        area.Y1 < area.Y2 &&
                        (area.Z1 is not { } low || area.Z2 is not { } high || low < high);

            if (!valid)
            {
                throw new InvalidDataException(
                    $"{file}: region '{region.Name}' has an area whose end is not past its start."
                );
            }
        }
    }

    private static void ValidateNamesAndParents(string file, List<RegionContent> regions)
    {
        var byName = new Dictionary<string, RegionContent>(StringComparer.Ordinal);

        foreach (var region in regions.Where(region => region.Name is not null))
        {
            if (!byName.TryAdd(region.Name!, region))
            {
                throw new InvalidDataException($"{file}: region name '{region.Name}' is used twice.");
            }
        }

        foreach (var region in regions.Where(region => region.Parent is not null))
        {
            var visited = new HashSet<RegionContent> { region };
            var parentName = region.Parent;

            while (parentName is not null)
            {
                if (!byName.TryGetValue(parentName, out var parent))
                {
                    throw new InvalidDataException(
                        $"{file}: region '{region.Name}' has parent '{parentName}', which is not in the file."
                    );
                }

                if (!visited.Add(parent))
                {
                    throw new InvalidDataException($"{file}: region '{region.Name}' is part of a parent loop.");
                }

                parentName = parent.Parent;
            }
        }
    }
}
