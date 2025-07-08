using Moongate.Core.Directories;
using Moongate.Core.Json;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Interfaces.FileLoaders;

namespace Moongate.UO.FileLoaders;

public class RegionDataLoader : IFileLoader
{
    private readonly DirectoriesConfig _directoriesConfig;

    public RegionDataLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public async Task LoadAsync()
    {
        var regionDataDirectory = Path.Combine(_directoriesConfig[DirectoryType.Data], "regions");

        var regionFiles = Directory.GetFiles(regionDataDirectory, "*.json");

        foreach (var regionFile in regionFiles)
        {
            var regionData = JsonUtils.DeserializeFromFile<JsonRegionWrap>(regionFile);
        }
    }
}
