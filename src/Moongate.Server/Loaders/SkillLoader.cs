using Moongate.Server.Interfaces;
using Moongate.UO.Data.Skills;
using Serilog;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Loaders;

/// <summary>
/// Loads skill definitions into <see cref="ISkillService" /> at startup: seeds the embedded
/// default <c>skills.yaml</c> into the data directory if it is missing, then parses and registers it.
/// </summary>
public sealed class SkillLoader : IDataLoader
{
    private readonly ILogger _logger = Log.ForContext<SkillLoader>();
    private readonly ISkillService _skills;
    private readonly DirectoriesConfig _directories;

    public SkillLoader(ISkillService skills, DirectoriesConfig directories)
    {
        _skills = skills;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var path = Path.Combine(dataDirectory, "skills.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(typeof(SkillLoader).Assembly, "Assets/skills.yaml");
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default skills.yaml at {Path}", path);
        }

        var definitions = YamlUtils.DeserializeFromFile<SkillDefinition[]>(path) ?? [];

        foreach (var definition in definitions)
        {
            _skills.Register(definition);
        }

        _logger.Information("Loaded {Count} skill definition(s) from {Path}", definitions.Length, path);

        return ValueTask.CompletedTask;
    }
}
