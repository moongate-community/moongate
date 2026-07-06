using Moongate.Network.Packets.Incoming;
using Moongate.Server.Data;
using Moongate.Server.Interfaces;

namespace Moongate.Server.Handlers;

/// <summary>Handles the login seed (0xEF): captures the connection seed.</summary>
public sealed class LoginSeedHandler : IPacketHandler<LoginSeedPacket>, IPacketHandlerRegistration
{
    public void Handle(LoginSeedPacket packet, in PacketContext context)
    {
        context.Session.SetSeed(packet.Seed);
    }

    public void Register(INetworkService network)
    {
        network.RegisterHandler(this);
    }
}
