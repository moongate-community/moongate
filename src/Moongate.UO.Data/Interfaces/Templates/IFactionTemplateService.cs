using Moongate.UO.Data.Templates.Factions;

namespace Moongate.UO.Data.Interfaces.Templates;

/// <summary>
/// Stores and resolves faction template definitions loaded from template files.
/// </summary>
public interface IFactionTemplateService
{
    /// <summary>
    /// Gets the number of registered faction templates.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Removes all registered faction templates.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets all faction templates as a snapshot list.
    /// </summary>
    /// <returns>All registered faction templates.</returns>
    IReadOnlyList<FactionDefinition> GetAll();

    /// <summary>
    /// Tries to resolve a faction template by id.
    /// </summary>
    /// <param name="id">Faction template identifier.</param>
    /// <param name="definition">Resolved definition when present.</param>
    /// <returns><c>true</c> when found; otherwise <c>false</c>.</returns>
    bool TryGet(string id, out FactionDefinition? definition);

    /// <summary>
    /// Adds or replaces a faction template by identifier.
    /// </summary>
    /// <param name="definition">Definition to register.</param>
    void Upsert(FactionDefinition definition);

    /// <summary>
    /// Adds or replaces multiple faction templates.
    /// </summary>
    /// <param name="definitions">Definitions to register.</param>
    void UpsertRange(IEnumerable<FactionDefinition> definitions);
}
