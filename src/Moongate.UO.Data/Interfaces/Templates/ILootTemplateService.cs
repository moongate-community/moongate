using Moongate.UO.Data.Templates.Loot;

namespace Moongate.UO.Data.Interfaces.Templates;

/// <summary>
/// Stores and resolves loot template definitions loaded from template files.
/// </summary>
public interface ILootTemplateService
{
    /// <summary>
    /// Gets the number of templates currently registered.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Removes all registered templates.
    /// </summary>
    void Clear();

    /// <summary>
    /// Returns all loot templates as a snapshot.
    /// </summary>
    /// <returns>List of currently registered loot templates.</returns>
    IReadOnlyList<LootTemplateDefinition> GetAll();

    /// <summary>
    /// Tries to get a loot template by id.
    /// </summary>
    /// <param name="id">Loot template id.</param>
    /// <param name="definition">Resolved template when present.</param>
    /// <returns><c>true</c> when found; otherwise <c>false</c>.</returns>
    bool TryGet(string id, out LootTemplateDefinition? definition);

    /// <summary>
    /// Adds or replaces a loot template by identifier.
    /// </summary>
    /// <param name="definition">Template instance to register.</param>
    void Upsert(LootTemplateDefinition definition);

    /// <summary>
    /// Adds or replaces multiple loot templates.
    /// </summary>
    /// <param name="templates">Templates to register.</param>
    void UpsertRange(IEnumerable<LootTemplateDefinition> templates);
}
