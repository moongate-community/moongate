using Moongate.UO.Data.Templates.SellProfiles;

namespace Moongate.UO.Data.Interfaces.Templates;

/// <summary>
/// Stores and resolves sell profile template definitions loaded from template files.
/// </summary>
public interface ISellProfileTemplateService
{
    /// <summary>
    /// Gets the number of registered sell profiles.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Removes all registered sell profiles.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets all sell profiles as a snapshot list.
    /// </summary>
    /// <returns>All registered sell profiles.</returns>
    IReadOnlyList<SellProfileTemplateDefinition> GetAll();

    /// <summary>
    /// Tries to resolve a sell profile by id.
    /// </summary>
    /// <param name="id">Sell profile id.</param>
    /// <param name="definition">Resolved sell profile when present.</param>
    /// <returns><c>true</c> when found; otherwise <c>false</c>.</returns>
    bool TryGet(string id, out SellProfileTemplateDefinition? definition);

    /// <summary>
    /// Adds or replaces a sell profile by identifier.
    /// </summary>
    /// <param name="definition">Sell profile to register.</param>
    void Upsert(SellProfileTemplateDefinition definition);

    /// <summary>
    /// Adds or replaces multiple sell profiles.
    /// </summary>
    /// <param name="definitions">Sell profiles to register.</param>
    void UpsertRange(IEnumerable<SellProfileTemplateDefinition> definitions);
}
