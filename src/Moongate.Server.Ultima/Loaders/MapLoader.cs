using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Internal;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

public class MapLoader : IDataLoader<MapContent>
{
    private readonly DirectoriesConfig _directoriesConfig;

    private readonly ILogger _logger = Log.ForContext<MapLoader>();

    private string mapsFilePath => Path.Join(_directoriesConfig["data"], "maps.toml");

    public MapLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(mapsFilePath))
        {
            throw new FileNotFoundException("Maps file maps.toml not found");
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<MapContent>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var mapsFile = await TomlUtils.DeserializeFromFileAsync<MapContentFile>(mapsFilePath, null, cancellationToken);

        _logger.Information("Found {Count} maps", mapsFile?.Map.Count ?? 0);

        return new DataLoaderResult<MapContent>()
        {
            Entities = mapsFile.Map
        };
    }
}
