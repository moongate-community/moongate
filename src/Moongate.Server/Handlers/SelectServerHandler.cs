using System.Net;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Server.Data;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces;

namespace Moongate.Server.Handlers;

/// <summary>Handles select server (0xA0): mints an auth key and redirects to the game port.</summary>
public sealed class SelectServerHandler : IPacketHandler<SelectServerPacket>, IPacketHandlerRegistration
{
    private readonly IPendingLoginStore _pendingLogins;
    private readonly MoongateConfig _config;

    public SelectServerHandler(IPendingLoginStore pendingLogins, MoongateConfig config)
    {
        _pendingLogins = pendingLogins;
        _config = config;
    }

    public void Handle(SelectServerPacket packet, in PacketContext context)
    {
        var authKey = _pendingLogins.Create(new PendingLogin(context.Session.Username ?? string.Empty));

        _ = context.Session.SendAsync(
            new ConnectToGameServerPacket(
                IPAddress.Parse(_config.Network.PublicAddress),
                (ushort)_config.Network.Port,
                authKey
            )
        );
    }

    public void Register(INetworkService network)
    {
        network.RegisterHandler(this);
    }
}
