using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Server.Data;
using Moongate.Server.Interfaces.Network;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles ping / keep-alive (0x73): echoes the client's sequence byte back so the connection stays
/// alive. Valid in any session state.
/// </summary>
public sealed class PingHandler : IPacketHandler<PingPacket>, IPacketHandlerRegistration
{
    public void Handle(PingPacket packet, in PacketContext context)
        => context.Session.Send(new PingAckPacket(packet.Sequence));

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
