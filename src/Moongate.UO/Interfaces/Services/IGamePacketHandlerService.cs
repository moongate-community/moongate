using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Interfaces.Handlers;

namespace Moongate.UO.Interfaces.Services;

public interface IGamePacketHandlerService : IMoongateService
{
    void RegisterGamePacketHandler<TPacket, TGamePacketHandler>()
        where TPacket : IUoNetworkPacket, new()
        where TGamePacketHandler : IGamePacketHandler;
}
