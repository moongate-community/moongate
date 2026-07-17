using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.UO.Data.Professions;

namespace Moongate.Server.Services.Mobiles;

/// <summary>
/// In-memory registry of profession presets, queryable by name. Populated at startup by
/// <see cref="Moongate.Server.Loaders.ProfessionsLoader" />.
/// </summary>
public sealed class ProfessionService : IProfessionService
{
    private readonly Dictionary<string, ProfessionDefinition> _byName = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ProfessionDefinition> All => [.. _byName.Values.OrderBy(definition => definition.Name)];

    public int Count => _byName.Count;

    public ProfessionDefinition? GetByName(string name)
        => _byName.GetValueOrDefault(name);

    public void Register(ProfessionDefinition definition)
        => _byName[definition.Name] = definition;
}
