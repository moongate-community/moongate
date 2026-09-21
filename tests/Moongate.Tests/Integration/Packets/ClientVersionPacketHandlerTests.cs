using DryIoc;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.Support.Sessions;

namespace Moongate.Tests.Integration.Packets;

public sealed class ClientVersionPacketHandlerTests
{
    [Fact]
    public async Task Dispatch_MutatesVersionOnlyWhenGameLoopProcessesPacket()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = new Container();
        container.RegisterPacketHandler<ClientVersionPacket, ClientVersionPacketHandler>();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var dispatcher = new PacketDispatchService(
            fixture.Loop,
            sessions,
            container.Resolve<PacketHandlerRegistry>(),
            container
        );
        await dispatcher.StartAsync();
        using var blocker = new BlockingGameLoopWorkItem();
        Assert.True(fixture.Loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(dispatcher.TryDispatch(session.SessionId, new ClientVersionPacket("7.0.98.1")));
        Assert.Null(session.NetworkSession.ClientVersion);
        blocker.Release();
        await fixture.ExecuteOnLoopAsync(() => { });
        Assert.Equal("7.0.98.1", session.NetworkSession.ClientVersion);
        await dispatcher.StopAsync();
    }
}
