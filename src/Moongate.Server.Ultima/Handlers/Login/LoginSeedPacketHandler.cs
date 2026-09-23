using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Login;

public class LoginSeedPacketHandler : IPacketHandler<LoginSeedPacket>
{
    private readonly ILogger _logger = Log.ForContext<LoginSeedPacketHandler>();

    public void Handle(GameSession session, LoginSeedPacket packet)
    {
        _logger.Information(
            "Client connected with version: v{Major}.{Minor}.{Revision}",
            packet.Major,
            packet.Minor,
            packet.Revision
        );
    }
}
