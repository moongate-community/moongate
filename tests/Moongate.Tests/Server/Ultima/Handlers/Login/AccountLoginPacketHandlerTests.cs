using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Packets;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Server.Ultima;

namespace Moongate.Tests.Server.Ultima.Handlers.Login;

public sealed class AccountLoginPacketHandlerTests
{
    [Fact]
    public async Task HandleAsync_UnknownAccount_SendsInvalidCredentials()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var middleware = new ControlledSendMiddleware();
        fixture.Client.AddMiddleware(middleware);
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service);
        var handler = new AccountLoginPacketHandler(sender, new RecordingAccountService());
        var context = new PacketContext(session, fixture.Loop, sessions, sender);
        await sender.StartAsync();

        try
        {
            await handler.HandleAsync(context, new AccountLoginPacket("tester", "secret", 0xFF), CancellationToken.None);

            Assert.Equal(new byte[] { 0x82, 0x03 }, await middleware.ReadAsync());
            Assert.True(fixture.Client.IsConnected);
        }
        finally
        {
            await sender.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task HandleAsync_RejectedReply_ClosesConnection()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service);
        var handler = new AccountLoginPacketHandler(sender, new RecordingAccountService());
        var context = new PacketContext(session, fixture.Loop, sessions, sender);

        await handler.HandleAsync(context, new AccountLoginPacket("tester", "secret", 0xFF), CancellationToken.None);

        await fixture.Client.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(fixture.Client.IsConnected);
        await sender.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

}
