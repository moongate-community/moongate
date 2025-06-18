using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Session;

namespace Moongate.UO.Interfaces.Handlers;

public interface IGamePacketHandler
{
    Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet);
}
