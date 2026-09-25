using Moongate.Core.Primitives;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;

namespace Moongate.Tests.Server.Core.Packets;

public sealed class PacketContextTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task RunOnGameLoopAsync_UpdatesOriginalSessionOnLoopThread()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var context = new PacketContext(session, fixture.Loop, sessions, new StubPacketSendService());
        var accountId = new Serial(42);
        var ranOnLoop = false;

        var applied = await context.RunOnGameLoopAsync(gameSession =>
                {
                    ranOnLoop = fixture.Loop.IsOnLoopThread;
                    gameSession.SetAccountId(accountId);
                }
            )
            .AsTask()
            .WaitAsync(Timeout);

        Assert.True(applied);
        Assert.True(ranOnLoop);
        Assert.Equal(accountId, session.AccountId);
    }

    [Fact]
    public async Task RunOnGameLoopAsync_DisconnectedBeforeExecutionSkipsAction()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var context = new PacketContext(session, fixture.Loop, sessions, new StubPacketSendService());
        using var blocker = new BlockingGameLoopWorkItem();
        Assert.True(fixture.Loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        var ran = false;
        var result = context.RunOnGameLoopAsync(_ => ran = true).AsTask();
        session.NetworkSession.DetachClient();
        blocker.Release();

        Assert.False(await result.WaitAsync(Timeout));
        Assert.False(ran);
    }

    [Fact]
    public async Task RunOnGameLoopAsync_ReplacementSessionCannotReceiveOldResult()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var original = sessions.GetOrCreate(fixture.Client);
        var context = new PacketContext(original, fixture.Loop, sessions, new StubPacketSendService());
        using var blocker = new BlockingGameLoopWorkItem();
        Assert.True(fixture.Loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        var ran = false;
        var result = context.RunOnGameLoopAsync(_ => ran = true).AsTask();
        Assert.True(sessions.Remove(original.SessionId));
        var replacement = sessions.GetOrCreate(fixture.Client);
        blocker.Release();

        Assert.NotSame(original, replacement);
        Assert.False(await result.WaitAsync(Timeout));
        Assert.False(ran);
    }

    [Fact]
    public async Task RunOnGameLoopAsync_CanceledBeforeAdmissionDoesNotRunAction()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var context = new PacketContext(session, fixture.Loop, sessions, new StubPacketSendService());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var ran = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            context.RunOnGameLoopAsync(_ => ran = true, cancellation.Token).AsTask()
        );
        Assert.False(ran);
    }

    [Fact]
    public async Task RunOnGameLoopAsync_ActionFailureReturnsToCallerWithoutFaultingLoop()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var context = new PacketContext(session, fixture.Loop, sessions, new StubPacketSendService());
        var failure = new InvalidOperationException("action failure");

        Assert.Same(
            failure,
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.RunOnGameLoopAsync(_ => throw failure).AsTask()
            )
        );
        Assert.False(fixture.Loop.Completion.IsCompleted);
    }

    [Fact]
    public async Task RunOnGameLoopAsync_AfterLoopStopsReturnsFalse()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var context = new PacketContext(session, fixture.Loop, sessions, new StubPacketSendService());
        await fixture.Loop.StopAsync().WaitAsync(Timeout);

        Assert.False(await context.RunOnGameLoopAsync(_ => throw new("must not run")));
    }

    [Fact]
    public async Task RunOnGameLoopAsync_OnLoopThreadRejectsWaiting()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var context = new PacketContext(session, fixture.Loop, sessions, new StubPacketSendService());

        await fixture.ExecuteOnLoopAsync(() =>
            {
                var result = context.RunOnGameLoopAsync(_ => { }).AsTask();
                Assert.True(result.IsFaulted);
                Assert.IsType<InvalidOperationException>(result.Exception!.InnerException);
            }
        );
        Assert.False(fixture.Loop.Completion.IsCompleted);
    }

    [Fact]
    public async Task TrySend_DisconnectedSessionDoesNotQueuePacket()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var sender = new StubPacketSendService();
        var context = new PacketContext(session, fixture.Loop, sessions, sender);
        var denial = new LoginDeniedPacket(LoginDeniedReason.CommunicationProblem);

        Assert.True(context.TrySend(denial));
        Assert.Same(fixture.Client, sender.ExpectedConnection);
        session.NetworkSession.DetachClient();
        Assert.False(context.TrySend(denial));
        Assert.Equal(1, sender.SentCount);
    }

    [Fact]
    public async Task SendAndDisconnectAsync_ClosesOriginalConnectionAfterFinalPacket()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var sender = new StubPacketSendService();
        var context = new PacketContext(session, fixture.Loop, sessions, sender);

        var sent = await context.SendAndDisconnectAsync(new LoginDeniedPacket(LoginDeniedReason.CommunicationProblem));

        Assert.True(sent);
        Assert.Equal(1, sender.SentCount);
        Assert.Same(fixture.Client, sender.ExpectedConnection);
        Assert.False(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task SendAndDisconnectAsync_ReplacedSessionCannotReceiveFinalPacket()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var original = sessions.GetOrCreate(fixture.Client);
        var sender = new StubPacketSendService();
        var context = new PacketContext(original, fixture.Loop, sessions, sender);
        Assert.True(sessions.Remove(original.SessionId));
        _ = sessions.GetOrCreate(fixture.Client);

        var sent = await context.SendAndDisconnectAsync(new LoginDeniedPacket(LoginDeniedReason.CommunicationProblem));

        Assert.False(sent);
        Assert.Equal(0, sender.SentCount);
    }

    [Fact]
    public async Task SendAndDisconnectAsync_RejectedTerminalPacketStillClosesConnection()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var sender = new StubPacketSendService { RejectTerminalSend = true };
        var context = new PacketContext(session, fixture.Loop, sessions, sender);

        var sent = await context.SendAndDisconnectAsync(new LoginDeniedPacket(LoginDeniedReason.CommunicationProblem));

        Assert.False(sent);
        Assert.Equal(0, sender.SentCount);
        Assert.False(fixture.Client.IsConnected);
    }
}
