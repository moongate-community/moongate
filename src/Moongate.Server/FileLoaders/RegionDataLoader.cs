using Moongate.Core.Data.Directories;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Json.Regions;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Loads ModernUO-style regions.json files (array of region entries).
/// </summary>
[RegisterFileLoader(16)]
public class RegionDataLoader : IFileLoader
{
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly ILogger _logger = Log.ForContext<RegionDataLoader>();

    public RegionDataLoader(DirectoriesConfig directoriesConfig, ISpatialWorldService spatialWorldService)
    {
        _directoriesConfig = directoriesConfig;
        _spatialWorldService = spatialWorldService;
    }

    public Task LoadAsync()
    {
        var regionDataDirectory = Path.Combine(_directoriesConfig[DirectoryType.Data], "regions");
        var regionFiles = Directory.GetFiles(regionDataDirectory, "*.json");

        foreach (var regionFile in regionFiles)
        {
            var regions = JsonUtils.DeserializeFromFile<JsonRegion[]>(
                regionFile,
                MoongateUOJsonSerializationContext.Default
            );
            var generatedId = 1;

            foreach (var region in regions)
            {
                if (region.Id == 0)
                {
                    region.Id = generatedId++;
                }

                _spatialWorldService.AddRegion(region);
            }

            _logger.Information(
                "Loaded {RegionCount} regions from file: {FilePath}",
                regions.Length,
                new FileInfo(regionFile).Name
            );
        }

        return Task.CompletedTask;
    }
}
