using Moongate.Network.Packets.General;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Tests.TestSupport.Login;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Server.Ultima.Handlers.Login;

public sealed class LoginRolePingPacketHandlerTests
{
    [Fact]
    public async Task HandleAsync_EchoesPingOnItsOwnConnection()
    {
        using var connection = new ControlledNetworkConnection(1);
        var session = new LoginSession(connection);
        var sender = new RecordingLoginPacketSender();
        var handler = new LoginRolePingPacketHandler(sender);

        await handler.HandleAsync(session, new(42), CancellationToken.None);

        Assert.Equal(42, Assert.IsType<PingPacket>(Assert.Single(sender.Sent)).Sequence);
    }
}
