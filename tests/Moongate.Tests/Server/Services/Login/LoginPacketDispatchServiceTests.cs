using DryIoc;
using Moongate.Network.Packets.General;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Services.Login;
using Moongate.Tests.TestSupport.Login;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Server.Services.Login;

public sealed class LoginPacketDispatchServiceTests
{
    [Fact]
    public async Task TryDispatch_ReplacementSessionId_DoesNotReuseOrCloseOldMailbox()
    {
        using var container = new Container();
        container.RegisterLoginPacketHandler<PingPacket, RecordingLoginPacketHandler>();
        var sessions = new LoginSessionService();
        using var first = new ControlledNetworkConnection(1);
        using var replacement = new ControlledNetworkConnection(1);
        var oldSession = sessions.GetOrCreate(first);
        var dispatcher = new LoginPacketDispatchService(
            sessions,
            container.Resolve<LoginPacketHandlerRegistry>(),
            container
        );
        await dispatcher.StartAsync();

        Assert.True(dispatcher.TryDispatch(1, new PingPacket(1)));
        Assert.True(sessions.Remove(oldSession));
        sessions.GetOrCreate(replacement);
        Assert.True(dispatcher.TryDispatch(1, new PingPacket(2)));
        await dispatcher.DisconnectAsync(oldSession);
        Assert.True(dispatcher.TryDispatch(1, new PingPacket(3)));
        await dispatcher.StopAsync();
    }

    [Fact]
    public async Task TryDispatch_AwaitsPerConnectionInOrder_WhileOtherConnectionsProgress()
    {
        using var container = new Container();
        container.RegisterLoginPacketHandler<PingPacket, RecordingLoginPacketHandler>();
        Assert.False(container.IsRegistered<IGameLoopService>());
        Assert.False(container.IsRegistered<ISessionService>());
        var sessions = new LoginSessionService();
        using var first = new ControlledNetworkConnection(1);
        using var second = new ControlledNetworkConnection(2);
        sessions.GetOrCreate(first);
        sessions.GetOrCreate(second);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new List<(long SessionId, byte Sequence)>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        container.Resolve<RecordingLoginPacketHandler>().OnHandle = async (session, packet, token) =>
                                                                    {
                                                                        if (session.SessionId == 1 && packet.Sequence == 1)
                                                                        {
                                                                            await gate.Task.WaitAsync(token);
                                                                        }

                                                                        lock (observed)
                                                                        {
                                                                            observed.Add(
                                                                                (session.SessionId, packet.Sequence)
                                                                            );

                                                                            if (observed.Count == 3)
                                                                            {
                                                                                completed.TrySetResult();
                                                                            }
                                                                        }
                                                                    };
        var dispatcher = new LoginPacketDispatchService(
            sessions,
            container.Resolve<LoginPacketHandlerRegistry>(),
            container
        );
        await dispatcher.StartAsync();

        Assert.True(dispatcher.TryDispatch(1, new PingPacket(1)));
        Assert.True(dispatcher.TryDispatch(1, new PingPacket(2)));
        Assert.True(dispatcher.TryDispatch(2, new PingPacket(3)));
        await Task.Delay(50);
        Assert.Contains((2L, (byte)3), observed);
        gate.TrySetResult();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal([(1L, (byte)1), (1L, (byte)2)], observed.Where(x => x.SessionId == 1));
        await dispatcher.StopAsync();
    }

    [Fact]
    public async Task TryDispatch_BoundsPendingFramesAndCancelsBlockedHandlerOnDisconnect()
    {
        using var container = new Container();
        container.RegisterLoginPacketHandler<PingPacket, RecordingLoginPacketHandler>();
        var sessions = new LoginSessionService();
        using var connection = new ControlledNetworkConnection(1);
        sessions.GetOrCreate(connection);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        container.Resolve<RecordingLoginPacketHandler>().OnHandle = async (_, _, token) =>
                                                                    {
                                                                        entered.TrySetResult();
                                                                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                                                                    };
        var dispatcher = new LoginPacketDispatchService(
            sessions,
            container.Resolve<LoginPacketHandlerRegistry>(),
            container
        );
        await dispatcher.StartAsync();
        Assert.True(dispatcher.TryDispatch(1, new PingPacket(0)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        for (var i = 0; i < 128; i++)
        {
            Assert.True(dispatcher.TryDispatch(1, new PingPacket((byte)i)));
        }

        Assert.False(dispatcher.TryDispatch(1, new PingPacket(129)));
        await dispatcher.DisconnectAsync(1).WaitAsync(TimeSpan.FromSeconds(2));
        await dispatcher.StopAsync();
    }
}
