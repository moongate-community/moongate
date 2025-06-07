using Moongate.Core.Server.Interfaces.Packets;

namespace Moongate.Core.Server.Interfaces.Handlers;

public interface INetworkPacketHandler
{
    Task HandlePacketAsync(string sessionId, IUoNetworkPacket packet);
}
