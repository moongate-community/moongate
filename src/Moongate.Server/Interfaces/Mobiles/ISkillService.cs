using Moongate.UO.Data.Skills;

namespace Moongate.Server.Interfaces.Mobiles;

/// <summary>
/// In-memory registry of skill definitions loaded from data/skills.yaml, queryable by id or name.
/// </summary>
public interface ISkillService
{
    /// <summary>All registered skill definitions, ordered by id.</summary>
    IReadOnlyList<SkillDefinition> All { get; }

    /// <summary>Number of registered skill definitions.</summary>
    int Count { get; }

    /// <summary>Adds or replaces a skill definition, indexing it by id and by name.</summary>
    void Register(SkillDefinition definition);

    /// <summary>Returns the definition with the given id, or null if none is registered.</summary>
    SkillDefinition? GetById(int id);

    /// <summary>Returns the definition with the given name (case-insensitive), or null if none.</summary>
    SkillDefinition? GetByName(string name);
}
