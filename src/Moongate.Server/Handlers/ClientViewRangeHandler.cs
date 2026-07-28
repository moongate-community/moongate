using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Network;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles client view range (0xC8): stores the update range the client asked for, clamped to what
/// the server allows, and echoes the accepted value back. Valid in any session state — it is a
/// client preference, not an action on the world.
/// </summary>
public sealed class ClientViewRangeHandler : IPacketHandler<ClientViewRangePacket>, IPacketHandlerRegistration
{
    public void Handle(ClientViewRangePacket packet, in PacketContext context)
    {
        context.Session.SetViewRange(packet.Range);

        // Reads the range back off the session rather than reusing packet.Range, so the reply can
        // never disagree with what was stored. The clamp keeps it inside 5..18, so the cast is safe.
        context.Session.Send(new ClientViewRangeAckPacket((byte)context.Session.ViewRange));
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
