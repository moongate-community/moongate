using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Session;

namespace Moongate.UO.Extensions;

public static class GameSessionExtensions
{
    public static void SendPackets(this GameSession session, params IUoNetworkPacket[] packets)
    {
        foreach (var packet in packets)
        {
            MoongateContext.NetworkService.SendPacket(session.NetworkClient, packet);
        }
    }

    public static void Disconnect(this GameSession session)
    {
        session.NetworkClient.Disconnect();
    }
}
