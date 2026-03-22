using System.Collections.Concurrent;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Templates.Factions;

namespace Moongate.UO.Data.Services.Templates;

/// <summary>
/// In-memory registry for faction templates keyed by template id.
/// </summary>
public sealed class FactionTemplateService : IFactionTemplateService
{
    private readonly ConcurrentDictionary<string, FactionDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _definitions.Count;

    public void Clear()
        => _definitions.Clear();

    public IReadOnlyList<FactionDefinition> GetAll()
        => _definitions.Values.OrderBy(static definition => definition.Id, StringComparer.OrdinalIgnoreCase).ToList();

    public bool TryGet(string id, out FactionDefinition? definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return _definitions.TryGetValue(id, out definition);
    }

    public void Upsert(FactionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);

        _definitions[definition.Id] = definition;
    }

    public void UpsertRange(IEnumerable<FactionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (var definition in definitions)
        {
            Upsert(definition);
        }
    }
}
