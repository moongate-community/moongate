using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Player;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.ClientViewRangePacket)]
public class ClientViewRangeHandler : BasePacketListener
{
    public ClientViewRangeHandler(IOutgoingPacketQueue outgoingPacketQueue)
        : base(outgoingPacketQueue) { }

    protected override Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is not ClientViewRangePacket clientViewRangePacket)
        {
            return Task.FromResult(true);
        }

        var clampedRange = ClampRange(clientViewRangePacket.Range);
        session.ViewRange = clampedRange;

        // Echo/relay back to client so update-range takes effect client-side.
        Enqueue(session, new ClientViewRangePacket(clampedRange));

        return Task.FromResult(true);
    }

    private static byte ClampRange(byte range)
    {
        if (range < ClientViewRangePacket.MinRange)
        {
            return ClientViewRangePacket.MinRange;
        }

        if (range > ClientViewRangePacket.MaxRange)
        {
            return ClientViewRangePacket.MaxRange;
        }

        return range;
    }
}
