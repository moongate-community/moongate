using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Login;

public sealed class LoginRoleSeedPacketHandler : ILoginPacketHandler<LoginSeedPacket>
{
    private readonly ILogger _logger = Log.ForContext<LoginRoleSeedPacketHandler>();

    public ValueTask HandleAsync(LoginSession session, LoginSeedPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        session.NetworkSession.SetSeed(packet.Seed);
        _logger.Information("Login client connected with version v{Major}.{Minor}.{Revision}",
            packet.Major, packet.Minor, packet.Revision);
        return ValueTask.CompletedTask;
    }
}
