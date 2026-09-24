using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Tests.TestSupport.Game;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Server.Ultima.Handlers.Login;

public sealed class LoginSeedPacketHandlerTests
{
    [Fact]
    public async Task Handle_VersionedSeed_StoresSeedOnGameSession()
    {
        await using var fixture = new GameCoordinatorFixture();
        using var connection = new ControlledNetworkConnection(1);
        var session = fixture.Sessions.GetOrCreate(connection);
        var packet = new LoginSeedPacket(0x12345678, 7, 0, 117, 0);

        new LoginSeedPacketHandler().Handle(session, packet);

        Assert.Equal(0x12345678u, session.NetworkSession.Seed);
    }
}
