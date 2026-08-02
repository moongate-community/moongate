using Moongate.Network.Packets.Incoming;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Gumps;
using Moongate.Server.Abstractions.Interfaces.Network;

namespace Moongate.Server.Handlers;

/// <summary>
/// Hands a gump response (0xB1) to the service, which is the only thing that knows what it drew and
/// so the only thing that can tell a genuine answer from an invented one.
/// </summary>
public sealed class GumpMenuSelectionHandler : IPacketHandler<GumpMenuSelectionPacket>, IPacketHandlerRegistration
{
    private readonly IGumpService _gumps;

    public GumpMenuSelectionHandler(IGumpService gumps)
    {
        _gumps = gumps;
    }

    public void Handle(GumpMenuSelectionPacket packet, in PacketContext context)

        // The verdict is the service's business -- it logs and disconnects on its own.
        => _gumps.HandleResponse(
            context.Session,
            packet.Serial,
            packet.TypeId,
            packet.ButtonId,
            packet.Switches,
            packet.TextEntries
        );

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
