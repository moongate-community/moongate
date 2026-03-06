using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;

namespace Moongate.Server.Interfaces.Services.Entities;

/// <summary>
/// Creates item entities from templates and fallback definitions.
/// </summary>
public interface IItemFactoryService
{
    /// <summary>
    /// Creates an item entity from an item template id.
    /// </summary>
    /// <param name="itemTemplateId">Item template identifier.</param>
    /// <returns>Initialized item entity with allocated serial.</returns>
    UOItemEntity CreateItemFromTemplate(string itemTemplateId);

    /// <summary>
    /// Creates a backpack item for newly created characters.
    /// </summary>
    /// <returns>Initialized backpack item entity with allocated serial.</returns>
    UOItemEntity GetNewBackpack();

    /// <summary>
    /// Tries to resolve an item template by id.
    /// </summary>
    /// <param name="itemTemplateId">Item template identifier.</param>
    /// <param name="definition">Resolved template when found.</param>
    /// <returns><see langword="true" /> when template exists; otherwise <see langword="false" />.</returns>
    bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? definition);
}
