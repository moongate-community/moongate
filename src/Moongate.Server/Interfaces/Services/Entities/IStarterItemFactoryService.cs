using Moongate.Server.Data.Entities;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Services.Entities;

/// <summary>
/// Creates starter inventory and equipment items for new characters.
/// </summary>
public interface IStarterItemFactoryService
{
    /// <summary>
    /// Creates a starter backpack for a mobile.
    /// </summary>
    /// <param name="mobileId">Owner mobile serial.</param>
    /// <param name="profileContext">Starter profile context.</param>
    /// <returns>Initialized backpack item entity with allocated serial.</returns>
    UOItemEntity CreateStarterBackpack(Serial mobileId, StarterProfileContext profileContext);

    /// <summary>
    /// Creates starter gold for a backpack.
    /// </summary>
    /// <param name="containerId">Backpack serial.</param>
    /// <param name="containerPosition">Position inside container.</param>
    /// <param name="quantity">Stack quantity.</param>
    /// <param name="profileContext">Starter profile context.</param>
    /// <returns>Initialized gold item entity with allocated serial.</returns>
    UOItemEntity CreateStarterGold(
        Serial containerId,
        Point2D containerPosition,
        int quantity,
        StarterProfileContext profileContext
    );

    /// <summary>
    /// Creates a starter equipped item for the given layer.
    /// </summary>
    /// <param name="mobileId">Owner mobile serial.</param>
    /// <param name="layer">Equipment layer.</param>
    /// <param name="profileContext">Starter profile context.</param>
    /// <returns>Initialized equipped item entity with allocated serial.</returns>
    UOItemEntity CreateStarterEquipment(Serial mobileId, ItemLayerType layer, StarterProfileContext profileContext);
}
