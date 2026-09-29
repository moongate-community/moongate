using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;

namespace Moongate.Tests.Server.Ultima.Handlers.General;

public sealed class UpdateRangePacketHandlerTests
{
    [Fact]
    public async Task Handle_AnswersWithTheServersViewRange()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);
        var sender = new StubPacketSendService();
        var handler = new UpdateRangePacketHandler(new WorldConfig { ViewRange = 20 }, sender);

        await fixture.ExecuteOnLoopAsync(() => handler.Handle(session, new UpdateRangePacket()));

        Assert.Equal(20, Assert.IsType<ViewRangePacket>(Assert.Single(sender.Sent)).Range);
    }
}
