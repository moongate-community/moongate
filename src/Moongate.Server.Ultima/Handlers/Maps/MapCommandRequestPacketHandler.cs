using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Server.Ultima.Handlers.Maps;

/// <summary>
///     Hands a player's change to the course of a map item (0x56) to the map service, which checks it.
/// </summary>
public sealed class MapCommandRequestPacketHandler : IPacketHandler<MapCommandRequestPacket>
{
    private readonly IMapDisplayService _maps;

    public MapCommandRequestPacketHandler(IMapDisplayService maps)
    {
        _maps = maps;
    }

    public void Handle(GameSession session, MapCommandRequestPacket packet)
    {
        _maps.Handle(session, packet);
    }
}
