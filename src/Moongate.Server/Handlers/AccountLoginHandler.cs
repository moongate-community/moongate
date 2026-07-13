using System.Net;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Server.Data;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Interfaces.Network;

namespace Moongate.Server.Handlers;

/// <summary>Handles account login (0x80): authenticates and returns the server list or a denial.</summary>
public sealed class AccountLoginHandler : IPacketHandler<AccountLoginRequestPacket>, IPacketHandlerRegistration
{
    private readonly IAccountService _accounts;
    private readonly MoongateConfig _config;

    public AccountLoginHandler(IAccountService accounts, MoongateConfig config)
    {
        _accounts = accounts;
        _config = config;
    }

    public void Handle(AccountLoginRequestPacket packet, in PacketContext context)
    {
        var result = _accounts.Authenticate(packet.Account, packet.Password);

        if (!result.Success)
        {
            context.Session.Send(new LoginDeniedPacket(result.Reason));

            return;
        }

        context.Session.MarkAuthenticated(result.Username);
        context.Session.Send(
            new ServerListPacket(_config.ShardName, IPAddress.Parse(_config.Network.PublicAddress))
        );
    }

    public void Register(INetworkService network)
    {
        network.RegisterHandler(this);
    }
}
