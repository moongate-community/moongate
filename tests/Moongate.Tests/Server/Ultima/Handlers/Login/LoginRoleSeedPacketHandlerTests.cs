using Moongate.Network.Packets.Data.Clients;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Server.Ultima.Handlers.Login;

public sealed class LoginRoleSeedPacketHandlerTests
{
    [Fact]
    public async Task HandleAsync_Seed_StoresTheSeedAndTheClientVersion()
    {
        using var connection = new ControlledNetworkConnection(1);
        var session = new LoginSession(connection);

        await new LoginRoleSeedPacketHandler().HandleAsync(
            session,
            new(0x12345678, 7, 0, 117, 0),
            CancellationToken.None
        );

        Assert.Equal(0x12345678u, session.NetworkSession.Seed);
        Assert.Equal(ClientVersion.Parse("7.0.117.0"), session.NetworkSession.ClientVersion);
    }
}
