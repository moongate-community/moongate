using Moongate.Core.Directories;
using Moongate.Core.Json;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Expansions;
using Moongate.UO.Interfaces.FileLoaders;
using Serilog;

namespace Moongate.UO.FileLoaders;

public class ExpansionLoader : IFileLoader
{

    private readonly DirectoriesConfig _directoriesConfig;
    private readonly ILogger _logger = Log.ForContext<ExpansionLoader>();

    public ExpansionLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public async Task LoadAsync()
    {
        var filePath = Path.Combine(_directoriesConfig[DirectoryType.Data], "expansions.json");

        var expansionsData = JsonUtils.DeserializeFromFile<ExpansionInfo[]>(filePath);

    }
}
