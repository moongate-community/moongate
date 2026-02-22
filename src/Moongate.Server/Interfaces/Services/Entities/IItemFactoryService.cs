using Moongate.UO.Data.Persistence.Entities;

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
}
