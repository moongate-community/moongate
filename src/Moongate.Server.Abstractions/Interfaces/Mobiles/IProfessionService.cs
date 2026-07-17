using Moongate.UO.Data.Professions;

namespace Moongate.Server.Abstractions.Interfaces.Mobiles;

/// <summary>In-memory registry of profession presets, queryable by name.</summary>
public interface IProfessionService
{
    /// <summary>All registered professions, ordered by name.</summary>
    IReadOnlyList<ProfessionDefinition> All { get; }

    /// <summary>Number of registered professions.</summary>
    int Count { get; }

    /// <summary>Returns the profession with the given name (case-insensitive), or null.</summary>
    ProfessionDefinition? GetByName(string name);

    /// <summary>Adds or replaces a profession, indexed by name.</summary>
    void Register(ProfessionDefinition definition);
}
