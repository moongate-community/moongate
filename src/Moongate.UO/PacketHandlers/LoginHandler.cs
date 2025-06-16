using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets;
using Moongate.UO.Data.Session;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Handlers;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class LoginHandler : IGamePacketHandler
{
    private readonly ILogger _logger = Log.ForContext<LoginHandler>();

    public async Task HandlePacketAsync(GameNetworkSession session, IUoNetworkPacket packet)
    {
        session.SendPackets(new LoginDeniedPacket(LoginDeniedReason.AccountAlreadyInUse));
    }
}
