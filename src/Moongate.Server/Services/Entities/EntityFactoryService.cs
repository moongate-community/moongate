using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Backward-compatible facade that delegates entity creation to specialized factory services.
/// </summary>
public sealed class EntityFactoryService : IEntityFactoryService
{
    private readonly IItemFactoryService _itemFactoryService;
    private readonly IMobileFactoryService _mobileFactoryService;

    public EntityFactoryService(
        IItemFactoryService itemFactoryService,
        IMobileFactoryService mobileFactoryService
    )
    {
        _itemFactoryService = itemFactoryService;
        _mobileFactoryService = mobileFactoryService;
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
    public UOItemEntity GetNewBackpack()
        => _itemFactoryService.GetNewBackpack();
}
