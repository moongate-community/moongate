using Moongate.Core.Data.Directories;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Interfaces.Services.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Json.Locations;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Loads map location catalogs from data/locations and derives map id from file names.
/// </summary>
[RegisterFileLoader(19)]
public class LocationsDataLoader : IFileLoader
{
    private static readonly IReadOnlyDictionary<string, int> MapIdByFileName =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["felucca"] = 0,
            ["trammel"] = 1,
            ["ilshenar"] = 2,
            ["malas"] = 3,
            ["tokuno"] = 4,
            ["termur"] = 5,
            ["internal"] = 0x7F
        };

    private readonly DirectoriesConfig _directoriesConfig;
    private readonly ILocationCatalogService _locationCatalogService;
    private readonly ILogger _logger = Log.ForContext<LocationsDataLoader>();

    public LocationsDataLoader(DirectoriesConfig directoriesConfig, ILocationCatalogService locationCatalogService)
    {
        _directoriesConfig = directoriesConfig;
        _locationCatalogService = locationCatalogService;
    }

    public Task LoadAsync()
    {
        var locationsDirectory = Path.Combine(_directoriesConfig[DirectoryType.Data], "locations");

        if (!Directory.Exists(locationsDirectory))
        {
            _logger.Warning("Locations directory not found at {Directory}.", locationsDirectory);
            _locationCatalogService.SetLocations([]);

            return Task.CompletedTask;
        }

        var files = Directory.GetFiles(locationsDirectory, "*.json", SearchOption.AllDirectories);
        var importedLocations = new List<WorldLocationEntry>();

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            if (!TryResolveMapId(fileName, out var mapId))
            {
                _logger.Warning(
                    "Skipping locations file {FilePath}: unsupported map file name '{MapFileName}'.",
                    filePath,
                    fileName
                );

                continue;
            }

            var mapLocations = JsonUtils.DeserializeFromFile<JsonMapLocations>(
                filePath,
                MoongateUOJsonSerializationContext.Default
            );
            var mapName = string.IsNullOrWhiteSpace(mapLocations.Name) ? fileName : mapLocations.Name;
            var beforeCount = importedLocations.Count;

            foreach (var category in mapLocations.Categories)
            {
                FlattenCategory(mapId, mapName, category, string.Empty, importedLocations);
            }

            var addedCount = importedLocations.Count - beforeCount;
            _logger.Information(
                "Loaded {Count} locations from file {FilePath} (MapId={MapId}).",
                addedCount,
                new FileInfo(filePath).Name,
                mapId
            );
        }

        _locationCatalogService.SetLocations(importedLocations);
        _logger.Information("Loaded {Count} total world locations.", importedLocations.Count);

        return Task.CompletedTask;
    }

    private static void FlattenCategory(
        int mapId,
        string mapName,
        JsonLocationCategory category,
        string parentPath,
        List<WorldLocationEntry> output
    )
    {
        var categoryName = category.Name?.Trim() ?? string.Empty;
        var categoryPath = string.IsNullOrWhiteSpace(parentPath) ? categoryName :
                           string.IsNullOrWhiteSpace(categoryName) ? parentPath : $"{parentPath} / {categoryName}";

        foreach (var location in category.Locations)
        {
            if (!TryParsePoint3D(location.Location, out var point))
            {
                continue;
            }

            output.Add(new(mapId, mapName, categoryPath, location.Name, point));
        }

        foreach (var childCategory in category.Categories)
        {
            FlattenCategory(mapId, mapName, childCategory, categoryPath, output);
        }
    }

    private static bool TryParsePoint3D(int[] coordinates, out Point3D location)
    {
        if (coordinates.Length < 3)
        {
            location = Point3D.Zero;

            return false;
        }

        location = new(coordinates[0], coordinates[1], coordinates[2]);

        return true;
    }

    private static bool TryResolveMapId(string fileName, out int mapId)
        => MapIdByFileName.TryGetValue(fileName.Trim(), out mapId);
}
