using Moongate.Core.Directories;
using Moongate.Core.Json;
using Moongate.Core.Server.Types;
using Moongate.UO.Context;
using Moongate.UO.Data.Skills;
using Moongate.UO.Interfaces.FileLoaders;
using Serilog;

namespace Moongate.UO.FileLoaders;

public class SkillLoader : IFileLoader
{
    private readonly DirectoriesConfig _directoriesConfig;

    private readonly ILogger _logger = Log.ForContext<SkillLoader>();

    public SkillLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public async Task LoadAsync()
    {
        UOContext.SkillsInfo =
            JsonUtils.DeserializeFromFile<SkillInfo[]>(Path.Combine(_directoriesConfig[DirectoryType.Data], "skills.json"));
        _logger.Information("Loaded {Count} skills from skills.json", UOContext.SkillsInfo?.Length ?? 0);
    }
}
