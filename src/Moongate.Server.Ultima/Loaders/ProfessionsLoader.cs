using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Professions;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the professions of <c>data/professions.toml</c>. Every starting skill must be a skill of
///     <c>skills.toml</c>, so this loader runs after <see cref="SkillsLoader" />; a profession with an id already used,
///     an id below 1 or an unknown skill stops the server at startup.
/// </summary>
public class ProfessionsLoader : IDataLoader<ProfessionContent>
{
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDataLoaderService _dataLoaderService;

    private readonly ILogger _logger = Log.ForContext<ProfessionsLoader>();

    private string professionsFilePath => Path.Join(_directoriesConfig["data"], "professions.toml");

    public ProfessionsLoader(DirectoriesConfig directoriesConfig, IDataLoaderService dataLoaderService)
    {
        _directoriesConfig = directoriesConfig;
        _dataLoaderService = dataLoaderService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(professionsFilePath))
        {
            throw new FileNotFoundException("Professions file professions.toml not found", professionsFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<ProfessionContent>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var professionsFile =
            await TomlUtils.DeserializeFromFileAsync<ProfessionContentFile>(
                professionsFilePath,
                null,
                cancellationToken
            );
        var professions = professionsFile?.Profession ?? [];

        if (professions.Count == 0)
        {
            throw new InvalidDataException($"{professionsFilePath} has no [[profession]] entries.");
        }

        var skillIds = _dataLoaderService.GetEntities<SkillContent>().Select(skill => skill.Id).ToHashSet();
        var professionIds = new HashSet<int>();

        foreach (var profession in professions)
        {
            if (profession.Id < 1 || !professionIds.Add(profession.Id))
            {
                throw new InvalidDataException(
                    $"{professionsFilePath}: profession '{profession.Name}' has id {profession.Id}; " +
                    "ids must be 1 or more and appear once (0 is the Advanced choice)."
                );
            }

            var unknownSkill = profession.Skills.FirstOrDefault(skill => !skillIds.Contains(skill.Skill));

            if (unknownSkill is not null)
            {
                throw new InvalidDataException(
                    $"{professionsFilePath}: profession '{profession.Name}' uses skill {unknownSkill.Skill}, " +
                    "which is not in skills.toml."
                );
            }
        }

        _logger.Information("Found {Count} professions", professions.Count);

        return new DataLoaderResult<ProfessionContent>()
        {
            Entities = professions
        };
    }
}
