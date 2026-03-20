using System.Collections.Concurrent;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Templates.Loot;

namespace Moongate.UO.Data.Services.Templates;

public sealed class LootTemplateService : ILootTemplateService
{
    private readonly ConcurrentDictionary<string, LootTemplateDefinition> _templates = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _templates.Count;

    public void Clear()
        => _templates.Clear();

    public IReadOnlyList<LootTemplateDefinition> GetAll()
        => _templates.Values.OrderBy(static template => template.Id, StringComparer.OrdinalIgnoreCase).ToList();

    public bool TryGet(string id, out LootTemplateDefinition? definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return _templates.TryGetValue(id, out definition);
    }

    public void Upsert(LootTemplateDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);

        _templates[definition.Id] = definition;
    }

    public void UpsertRange(IEnumerable<LootTemplateDefinition> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);

        foreach (var definition in templates)
        {
            Upsert(definition);
        }
    }
}
