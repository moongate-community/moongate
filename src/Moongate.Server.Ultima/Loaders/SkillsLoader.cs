using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the skills of <c>data/skills.toml</c>. The client identifies a skill by its id, so the file must list
///     ids 0, 1, 2 and so on in order, without gaps; anything else stops the server at startup.
/// </summary>
public class SkillsLoader : IDataLoader<SkillContent>
{
    private readonly DirectoriesConfig _directoriesConfig;

    private readonly ILogger _logger = Log.ForContext<SkillsLoader>();

    private string skillsFilePath => Path.Join(_directoriesConfig["data"], "skills.toml");

    public SkillsLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(skillsFilePath))
        {
            throw new FileNotFoundException("Skills file skills.toml not found", skillsFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<SkillContent>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var skillsFile =
            await TomlUtils.DeserializeFromFileAsync<SkillContentFile>(skillsFilePath, null, cancellationToken);
        var skills = skillsFile?.Skill ?? [];

        if (skills.Count == 0)
        {
            throw new InvalidDataException($"{skillsFilePath} has no [[skill]] entries.");
        }

        for (var index = 0; index < skills.Count; index++)
        {
            if ((int)skills[index].Id != index)
            {
                throw new InvalidDataException(
                    $"{skillsFilePath}: entry {index} has id {(int)skills[index].Id}, expected {index}. " +
                    "Skill ids must start at 0 and follow the order of the file."
                );
            }
        }

        _logger.Information("Found {Count} skills", skills.Count);

        return new DataLoaderResult<SkillContent>()
        {
            Entities = skills
        };
    }
}
