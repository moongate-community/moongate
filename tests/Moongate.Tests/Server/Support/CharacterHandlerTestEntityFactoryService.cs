using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Support;

public sealed class CharacterHandlerTestEntityFactoryService : IEntityFactoryService
{
    public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
        => throw new NotSupportedException();

    public UOMobileEntity CreateMobileFromTemplate(string mobileTemplateId, Serial? accountId = null)
        => throw new NotSupportedException();

    public UOMobileEntity CreatePlayerMobile(CharacterCreationPacket packet, Serial accountId)
        => packet.ToEntity(Serial.Zero, accountId);

    public UOItemEntity GetNewBackpack()
        => throw new NotSupportedException();
}
