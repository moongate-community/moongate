using Moongate.Server.Interfaces;
using Moongate.UO.Data.Skills;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Server.Services;

/// <summary>
/// Loads skill definitions from data/skills.yaml at startup and exposes them as a registry
/// keyed by id and by name.
/// </summary>
public sealed class SkillService : ISkillService, ISquidStdService
{
    private readonly ILogger _logger = Log.ForContext<SkillService>();
    private readonly DirectoriesConfig _directories;
    private readonly Dictionary<int, SkillDefinition> _byId = new();
    private readonly Dictionary<string, SkillDefinition> _byName = new(StringComparer.OrdinalIgnoreCase);

    public SkillService(DirectoriesConfig directories)
    {
        _directories = directories;
    }

    public IReadOnlyList<SkillDefinition> All
    {
        get
        {
            return _byId.Values.OrderBy(definition => definition.Id).ToList();
        }
    }

    public int Count
    {
        get
        {
            return _byId.Count;
        }
    }

    public void Register(SkillDefinition definition)
    {
        _byId[definition.Id] = definition;
        _byName[definition.Name] = definition;
    }

    public SkillDefinition? GetById(int id)
    {
        return _byId.TryGetValue(id, out var definition) ? definition : null;
    }

    public SkillDefinition? GetByName(string name)
    {
        return _byName.TryGetValue(name, out var definition) ? definition : null;
    }

    public void LoadFromFile(string path)
    {
        var definitions = YamlUtils.DeserializeFromFile<SkillDefinition[]>(path) ?? [];

        foreach (var definition in definitions)
        {
            Register(definition);
        }

        _logger.Information("Loaded {Count} skill definition(s) from {Path}", definitions.Length, path);
    }

    public void SeedAndLoad(string dataDirectory)
    {
        var path = Path.Combine(dataDirectory, "skills.yaml");

        if (!File.Exists(path))
        {
            var seed = ResourceUtils.GetEmbeddedResourceString(typeof(SkillService).Assembly, "Assets/skills.yaml");
            File.WriteAllText(path, seed);
            _logger.Information("Seeded default skills.yaml at {Path}", path);
        }

        LoadFromFile(path);
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        SeedAndLoad(_directories.RegisterDirectory("data"));
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
