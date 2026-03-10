using System.Collections.Concurrent;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Templates.SellProfiles;

namespace Moongate.UO.Data.Services.Templates;

/// <summary>
/// In-memory registry for sell profile templates keyed by profile id.
/// </summary>
public sealed class SellProfileTemplateService : ISellProfileTemplateService
{
    private readonly ConcurrentDictionary<string, SellProfileTemplateDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _definitions.Count;

    public void Clear()
        => _definitions.Clear();

    public IReadOnlyList<SellProfileTemplateDefinition> GetAll()
        => _definitions.Values.OrderBy(static definition => definition.Id, StringComparer.OrdinalIgnoreCase).ToList();

    public bool TryGet(string id, out SellProfileTemplateDefinition? definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return _definitions.TryGetValue(id, out definition);
    }

    public void Upsert(SellProfileTemplateDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);

        _definitions[definition.Id] = definition;
    }

    public void UpsertRange(IEnumerable<SellProfileTemplateDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (var definition in definitions)
        {
            Upsert(definition);
        }
    }
}
