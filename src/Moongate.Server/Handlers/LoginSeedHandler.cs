using Moongate.Network.Packets.Incoming;
using Moongate.Server.Data;
using Moongate.Server.Interfaces;
using Moongate.Ultima.Io;
using Moongate.UO.Data.Extensions;
using Moongate.UO.Data.Version;
using Serilog;

namespace Moongate.Server.Handlers;

/// <summary>Handles the login seed (0xEF): captures the connection seed.</summary>
public sealed class LoginSeedHandler : IPacketHandler<LoginSeedPacket>, IPacketHandlerRegistration
{
    private readonly ILogger _logger = Log.ForContext<LoginSeedHandler>();

    public void Handle(LoginSeedPacket packet, in PacketContext context)
    {
        var remoteClientVersion = new ClientVersion(
            (int)packet.Major,
            (int)packet.Minor,
            (int)packet.Revision,
            (int)packet.Prototype
        );

        // Null when client.exe is not configured/found: we can't enforce a version, so let it through.
        var localClientVersion = ClientVersionReader.Read()?.ToClientVersion();

        if (localClientVersion is not null && localClientVersion != remoteClientVersion)
        {
            _logger.Error(
                "Client version mismatch: local {LocalVersion}, remote {RemoteVersion}",
                localClientVersion,
                remoteClientVersion
            );

            context.Session.Disconnect();

            return;
        }

        context.Session.SetSeed(packet.Seed);
        context.Session.SetVersion(remoteClientVersion);
    }

    public void Register(INetworkService network)
    {
        network.RegisterHandler(this);
    }
}
