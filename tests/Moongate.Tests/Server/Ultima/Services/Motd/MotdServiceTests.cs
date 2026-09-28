using System.Net;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Admin;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services.Motd;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services.Motd;

public sealed class MotdServiceTests
{
    [Fact]
    public async Task SendAsync_SendsPrivateLinesWithPlayerAndLocalOnlineCount()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var entrant = sessions.GetOrCreate(fixture.Client);
        using var connected = new ControlledNetworkConnection(9001);
        using var characterless = new ControlledNetworkConnection(9002);
        using var disconnected = new ControlledNetworkConnection(9003);
        var other = sessions.GetOrCreate(connected);
        sessions.GetOrCreate(characterless);
        var gone = sessions.GetOrCreate(disconnected);
        await fixture.ExecuteOnLoopAsync(() =>
        {
            entrant.Set(SessionKeys.CharacterId, new Serial(2));
            other.Set(SessionKeys.CharacterId, new Serial(3));
            gone.Set(SessionKeys.CharacterId, new Serial(4));
        });
        disconnected.Complete();
        var sender = new StubPacketSendService();
        var context = new PacketContext(entrant, fixture.Loop, sessions, sender);
        var registry = new MotdVariableRegistry();
        MotdRenderer.RegisterBuiltins(registry);
        var service = CreateService(sessions, registry, new MotdLine(1, "Hello ${player_name}"), new MotdLine(2, "Online ${users_online}"));

        await service.SendAsync(context, new MobileEntity { Id = new Serial(2), Name = "Aria" }, CancellationToken.None);

        Assert.Equal(["Hello Aria", "Online 2"], sender.Sent.Cast<UnicodeSpeechMessagePacket>().Select(packet => packet.Text));
        Assert.All(sender.SentSessionIds, id => Assert.Equal(entrant.SessionId, id));
    }

    [Fact]
    public async Task SendAsync_SkipsResolverFailuresAndInvalidText_ButSendsLaterLines()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var entrant = sessions.GetOrCreate(fixture.Client);
        await fixture.ExecuteOnLoopAsync(() => entrant.Set(SessionKeys.CharacterId, new Serial(2)));
        var sender = new StubPacketSendService();
        var context = new PacketContext(entrant, fixture.Loop, sessions, sender);
        var registry = new MotdVariableRegistry();
        registry.Register("broken", (_, _) => throw new InvalidOperationException("private resolver detail"));
        registry.Register("nul", (_, _) => ValueTask.FromResult("bad\0text"));
        var service = CreateService(sessions, registry, new MotdLine(1, "${broken}"),
            new MotdLine(2, "${nul}"), new MotdLine(3, "Visible"));

        await service.SendAsync(context, new MobileEntity { Id = new Serial(2), Name = "Aria" }, CancellationToken.None);

        Assert.Equal(["Visible"], sender.Sent.Cast<UnicodeSpeechMessagePacket>().Select(packet => packet.Text));
    }

    [Fact]
    public async Task SendAsync_ReplacedSessionDuringResolver_StopsDelivery()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var entrant = sessions.GetOrCreate(fixture.Client);
        await fixture.ExecuteOnLoopAsync(() => entrant.Set(SessionKeys.CharacterId, new Serial(2)));
        var sender = new StubPacketSendService();
        var context = new PacketContext(entrant, fixture.Loop, sessions, sender);
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new MotdVariableRegistry();
        registry.Register("delayed", async (_, _) =>
        {
            started.SetResult();
            return await pending.Task;
        });
        var service = CreateService(sessions, registry, new MotdLine(1, "${delayed}"), new MotdLine(2, "Later"));

        var sending = service.SendAsync(context, new MobileEntity { Id = new Serial(2), Name = "Aria" }, CancellationToken.None).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        sessions.Remove(entrant.SessionId);
        sessions.GetOrCreate(fixture.Client);
        pending.SetResult("Old session");
        await sending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(sender.Sent);
    }

    private static MotdService CreateService(SessionService sessions, MotdVariableRegistry registry, params MotdLine[] lines)
    {
        var realm = new RealmInstance(new RealmDescriptor("local", 0, "Felucca", IPAddress.Loopback, 2593, AccountType.Regular), Guid.NewGuid());
        return new MotdService(new StubDataLoaderService().With(lines), new MotdRenderer(registry), sessions,
            new AdminServerInfoProvider(Moongate.Server.Core.Types.Hosting.ServerMode.Game, realm), realm,
            new MotdServerIdentity("Shard"));
    }
}
