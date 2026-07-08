using Moongate.Server.Interfaces;
using Moongate.UO.Data.Skills;

namespace Moongate.Server.Services;

/// <summary>
/// In-memory registry of skill definitions, queryable by id and by name. Populated at startup by
/// <see cref="Moongate.Server.Loaders.SkillLoader" />.
/// </summary>
public sealed class SkillService : ISkillService
{
    private readonly Dictionary<int, SkillDefinition> _byId = new();
    private readonly Dictionary<string, SkillDefinition> _byName = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<SkillDefinition> All => [.. _byId.Values.OrderBy(definition => definition.Id)];

    public int Count => _byId.Count;

    public void Register(SkillDefinition definition)
    {
        _byId[definition.Id] = definition;
        _byName[definition.Name] = definition;
    }

    public SkillDefinition? GetById(int id)
    {
        return _byId.GetValueOrDefault(id);
    }

    public SkillDefinition? GetByName(string name)
    {
        return _byName.GetValueOrDefault(name);
    }
}
