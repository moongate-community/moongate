using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Server.Data;
using Moongate.Server.Interfaces.Network;
using Moongate.Server.Interfaces.World;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles mega cliloc requests (0xD6): answers each requested serial with its property list.
/// Unknown serials are skipped silently — the client probes freely for anything it draws.
/// </summary>
public sealed class MegaClilocHandler : IPacketHandler<MegaClilocRequestPacket>, IPacketHandlerRegistration
{
    private readonly IOplService _opl;

    public MegaClilocHandler(IOplService opl)
    {
        _opl = opl;
    }

    public void Handle(MegaClilocRequestPacket packet, in PacketContext context)
    {
        foreach (var response in BuildResponses(packet.Serials, _opl))
        {
            context.Session.Send(response);
        }
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);

    /// <summary>The 0xD6 responses for a batch of requested serials; serials nothing is known about yield none.</summary>
    public static IEnumerable<MegaClilocPacket> BuildResponses(IReadOnlyList<Serial> serials, IOplService opl)
    {
        foreach (var serial in serials)
        {
            var snapshot = opl.GetOrBuild(serial);

            if (snapshot.HasEntries)
            {
                yield return new MegaClilocPacket(serial, snapshot.Hash, snapshot.Entries);
            }
        }
    }
}
