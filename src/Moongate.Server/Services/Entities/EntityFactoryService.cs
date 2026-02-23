using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Data.Entities;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Backward-compatible facade that delegates entity creation to specialized factory services.
/// </summary>
public sealed class EntityFactoryService : IEntityFactoryService
{
    private readonly IItemFactoryService _itemFactoryService;
    private readonly IMobileFactoryService _mobileFactoryService;
    private readonly IStarterItemFactoryService _starterItemFactoryService;

    public EntityFactoryService(
        IItemFactoryService itemFactoryService,
        IMobileFactoryService mobileFactoryService,
        IStarterItemFactoryService starterItemFactoryService
    )
    {
        _itemFactoryService = itemFactoryService;
        _mobileFactoryService = mobileFactoryService;
        _starterItemFactoryService = starterItemFactoryService;
    }

    /// <inheritdoc />
    public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
        => _itemFactoryService.CreateItemFromTemplate(itemTemplateId);

    /// <inheritdoc />
    public UOMobileEntity CreateMobileFromTemplate(string mobileTemplateId, Serial? accountId = null)
        => _mobileFactoryService.CreateMobileFromTemplate(mobileTemplateId, accountId);

    /// <inheritdoc />
    public UOMobileEntity CreatePlayerMobile(CharacterCreationPacket packet, Serial accountId)
        => _mobileFactoryService.CreatePlayerMobile(packet, accountId);

    /// <inheritdoc />
    public UOItemEntity CreateStarterBackpack(Serial mobileId, StarterProfileContext profileContext)
        => _starterItemFactoryService.CreateStarterBackpack(mobileId, profileContext);

    /// <inheritdoc />
    public UOItemEntity CreateStarterEquipment(Serial mobileId, ItemLayerType layer, StarterProfileContext profileContext)
        => _starterItemFactoryService.CreateStarterEquipment(mobileId, layer, profileContext);

    /// <inheritdoc />
    public UOItemEntity CreateStarterGold(
        Serial containerId,
        Point2D containerPosition,
        int quantity,
        StarterProfileContext profileContext
    )
        => _starterItemFactoryService.CreateStarterGold(containerId, containerPosition, quantity, profileContext);

    /// <inheritdoc />
    public UOItemEntity GetNewBackpack()
        => _itemFactoryService.GetNewBackpack();
}
