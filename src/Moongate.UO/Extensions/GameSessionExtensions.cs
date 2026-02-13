using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets.Data;
using Moongate.UO.Data.Session;
using ZLinq;

namespace Moongate.UO.Extensions;

public static class GameSessionExtensions
{
    public static void Disconnect(this GameSession session)
    {
        session.NetworkClient.Disconnect();
    }

    public static List<CharacterEntry> GetCharactersEntries(this GameSession session)
    {
        return session.Account
                      .Characters
                      .AsValueEnumerable()
                      .OrderBy(s => s.Slot)
                      .Select(s => new CharacterEntry(s.Name))
                      .ToList();
    }

    public static void SendPackets(this GameSession session, params IUoNetworkPacket[] packets)
    {
        foreach (var packet in packets)
        {
            MoongateContext.NetworkService.SendPacket(session.NetworkClient, packet);
        }
    }
}
