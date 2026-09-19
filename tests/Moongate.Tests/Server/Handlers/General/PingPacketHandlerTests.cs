using Moongate.Server.Services.Network;
using Moongate.Tests.TestSupport.Network;
using Moongate.Network.Packets.General;
using Moongate.Server.Handlers.General;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;

namespace Moongate.Tests.Server.Handlers.General;

public sealed class PingPacketHandlerTests
{
    [Fact]
    public async Task Handle_EchoesExactSequenceThroughQueuedSender()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var middleware = new ControlledSendMiddleware();
        fixture.Client.AddMiddleware(middleware);
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service);
        var handler = new PingPacketHandler(sender);
        await sender.StartAsync();
        try
        {
            await fixture.ExecuteOnLoopAsync(() => handler.Handle(session, new PingPacket(0xA7)));
            Assert.Equal(new byte[] { 0x73, 0xA7 }, await middleware.ReadAsync());
        }
        finally
        {
            await sender.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task Handle_RejectedReplyRequestsConnectionClosure()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service);
        var handler = new PingPacketHandler(sender);
        await fixture.ExecuteOnLoopAsync(() => handler.Handle(session, new PingPacket(3)));
        await fixture.Client.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(fixture.Client.IsConnected);
        await sender.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
