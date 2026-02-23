using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Data.Entities;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Services.Entities;

/// <summary>
/// Creates runtime entities from packets and template definitions.
/// </summary>
public interface IEntityFactoryService
{
    /// <summary>
    /// Creates an item entity from an item template id.
    /// </summary>
    /// <param name="itemTemplateId">Item template identifier.</param>
    /// <returns>Initialized item entity with allocated serial.</returns>
    UOItemEntity CreateItemFromTemplate(string itemTemplateId);

    /// <summary>
    /// Creates a mobile entity from a mobile template id.
    /// </summary>
    /// <param name="mobileTemplateId">Mobile template identifier.</param>
    /// <param name="accountId">Optional owner account identifier for player mobiles.</param>
    /// <returns>Initialized mobile entity with allocated serial.</returns>
    UOMobileEntity CreateMobileFromTemplate(string mobileTemplateId, Serial? accountId = null);

    /// <summary>
    /// Creates a player mobile from character creation packet data.
    /// </summary>
    /// <param name="packet">Character creation packet.</param>
    /// <param name="accountId">Owner account serial identifier.</param>
    /// <returns>Initialized mobile entity with allocated serial.</returns>
    UOMobileEntity CreatePlayerMobile(CharacterCreationPacket packet, Serial accountId);

    /// <summary>
    /// Creates a starter backpack for a mobile.
    /// </summary>
    /// <param name="mobileId">Owner mobile serial.</param>
    /// <param name="profileContext">Starter profile context.</param>
    /// <returns>Initialized backpack item entity with allocated serial.</returns>
    UOItemEntity CreateStarterBackpack(Serial mobileId, StarterProfileContext profileContext);

    /// <summary>
    /// Creates a starter equipped item for the given layer.
    /// </summary>
    /// <param name="mobileId">Owner mobile serial.</param>
    /// <param name="layer">Equipment layer.</param>
    /// <param name="profileContext">Starter profile context.</param>
    /// <returns>Initialized equipped item entity with allocated serial.</returns>
    UOItemEntity CreateStarterEquipment(Serial mobileId, ItemLayerType layer, StarterProfileContext profileContext);

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
    /// Creates a backpack item for newly created characters.
    /// </summary>
    /// <returns>Initialized backpack item entity with allocated serial.</returns>
    UOItemEntity GetNewBackpack();
}
