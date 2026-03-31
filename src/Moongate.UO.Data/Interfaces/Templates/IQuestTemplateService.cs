using Moongate.UO.Data.Templates.Quests;

namespace Moongate.UO.Data.Interfaces.Templates;

/// <summary>
/// Stores and resolves quest template definitions loaded from template files.
/// </summary>
public interface IQuestTemplateService
{
    /// <summary>
    /// Gets the number of registered quest templates.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Removes all registered quest templates.
    /// </summary>
    void Clear();

    /// <summary>
    /// Replaces all registered quest templates with the provided snapshot.
    /// </summary>
    /// <param name="definitions">Quest templates to restore.</param>
    void ReplaceAll(IEnumerable<QuestTemplateDefinition> definitions);

    /// <summary>
    /// Gets all quest templates as a snapshot list.
    /// </summary>
    /// <returns>All registered quest templates.</returns>
    IReadOnlyList<QuestTemplateDefinition> GetAll();

    /// <summary>
    /// Tries to resolve a quest template by id.
    /// </summary>
    /// <param name="id">Quest template id.</param>
    /// <param name="definition">Resolved quest template when present.</param>
    /// <returns><c>true</c> when found; otherwise <c>false</c>.</returns>
    bool TryGet(string id, out QuestTemplateDefinition? definition);

    /// <summary>
    /// Adds or replaces a quest template by identifier.
    /// </summary>
    /// <param name="definition">Quest template to register.</param>
    void Upsert(QuestTemplateDefinition definition);

    /// <summary>
    /// Adds or replaces multiple quest templates.
    /// </summary>
    /// <param name="definitions">Quest templates to register.</param>
    void UpsertRange(IEnumerable<QuestTemplateDefinition> definitions);
}
