using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Movement;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.NewMovementRequestPacket)]
public sealed class KrriosClientSpecialHandler : BasePacketListener
{
    public KrriosClientSpecialHandler(IOutgoingPacketQueue outgoingPacketQueue)
        : base(outgoingPacketQueue) { }

    protected override Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
        => Task.FromResult(packet is NewMovementRequestPacket);
}
