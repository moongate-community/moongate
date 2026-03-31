using System.Collections.Concurrent;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Templates.Quests;

namespace Moongate.UO.Data.Services.Templates;

/// <summary>
/// In-memory registry for quest templates keyed by template id.
/// </summary>
public sealed class QuestTemplateService : IQuestTemplateService
{
    private readonly ConcurrentDictionary<string, QuestTemplateDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _definitions.Count;

    public void Clear()
        => _definitions.Clear();

    public IReadOnlyList<QuestTemplateDefinition> GetAll()
        => _definitions.Values.OrderBy(static definition => definition.Id, StringComparer.OrdinalIgnoreCase).ToList();

    public bool TryGet(string id, out QuestTemplateDefinition? definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return _definitions.TryGetValue(id, out definition);
    }

    public void Upsert(QuestTemplateDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);

        _definitions[definition.Id] = definition;
    }

    public void UpsertRange(IEnumerable<QuestTemplateDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (var definition in definitions)
        {
            Upsert(definition);
        }
    }
}
