using Moongate.Server.Interfaces;
using Moongate.UO.Data.Skills;
using SquidStd.Core.Directories;

namespace Moongate.Server.Services;

/// <summary>
/// Loads skill definitions from data/skills.yaml at startup and exposes them as a registry
/// keyed by id and by name.
/// </summary>
public sealed class SkillService : ISkillService
{
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
}
