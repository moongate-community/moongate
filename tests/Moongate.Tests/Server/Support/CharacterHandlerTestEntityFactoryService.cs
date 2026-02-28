using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Data.Entities;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Support;

public sealed class CharacterHandlerTestEntityFactoryService : IEntityFactoryService
{
    public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
        => throw new NotSupportedException();

    public UOMobileEntity CreateMobileFromTemplate(string mobileTemplateId, Serial? accountId = null)
        => throw new NotSupportedException();

    public UOMobileEntity CreatePlayerMobile(CharacterCreationPacket packet, Serial accountId)
        => packet.ToEntity(Serial.Zero, accountId);

    public UOItemEntity CreateStarterBackpack(Serial mobileId, StarterProfileContext profileContext)
        => throw new NotSupportedException();

    public UOItemEntity CreateStarterEquipment(Serial mobileId, ItemLayerType layer, StarterProfileContext profileContext)
        => throw new NotSupportedException();

    public UOItemEntity CreateStarterGold(
        Serial containerId,
        Point2D containerPosition,
        int quantity,
        StarterProfileContext profileContext
    )
        => throw new NotSupportedException();

    public UOItemEntity GetNewBackpack()
        => throw new NotSupportedException();
}
