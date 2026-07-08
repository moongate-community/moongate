using Moongate.Network.Packets.Incoming;
using Moongate.Server.Data;
using Moongate.Server.Interfaces;
using Moongate.UO.Data.Version;

namespace Moongate.Server.Handlers;

/// <summary>Handles the login seed (0xEF): captures the connection seed.</summary>
public sealed class LoginSeedHandler : IPacketHandler<LoginSeedPacket>, IPacketHandlerRegistration
{
    public void Handle(LoginSeedPacket packet, in PacketContext context)
    {
        context.Session.SetSeed(packet.Seed);
        context.Session.SetVersion(new ClientVersion((int)packet.Major, (int)packet.Minor, (int)packet.Revision, (int)packet.Prototype));
    }

    public void Register(INetworkService network)
    {
        network.RegisterHandler(this);
    }
}
