using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Commands;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class ShutdownCommandTests
{
    [Theory]
    [InlineData("")]
    [InlineData("0")]
    public async Task ExecuteAsync_ImmediateShutdownAnnouncesBeforeRequestingStop(string arguments)
    {
        await using var fixture = await ShutdownCommandFixture.CreateAsync();
        fixture.World.Sender.OnSent = _ => Assert.False(fixture.Shutdown.Requested.IsCompleted);
        var context = fixture.Context(arguments);

        await fixture.Command.ExecuteAsync(context);

        Assert.True(fixture.Shutdown.Requested.IsCompletedSuccessfully);
        Assert.Equal(0, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
        var packet = Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.World.Sender.Sent));
        Assert.Equal("The server is shutting down now.", packet.Text);
        Assert.Equal(packet.Text, Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ExecuteAsync_DelayedShutdownReturnsAndSignalsOnlyAtDeadline()
    {
        await using var fixture = await ShutdownCommandFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();

        await fixture.Command.ExecuteAsync(fixture.Context("30", cancellation.Token)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(fixture.Shutdown.Requested.IsCompleted);
        Assert.Equal("The server will shut down in 30 seconds.",
            Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.World.Sender.Sent)).Text);
        cancellation.Cancel();
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(29));
        Assert.False(fixture.Shutdown.Requested.IsCompleted);
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        Assert.True(fixture.Shutdown.Requested.IsCompletedSuccessfully);
        Assert.Equal(0, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("2147483648")]
    [InlineData("10 extra")]
    public async Task ExecuteAsync_InvalidArgumentsHaveNoSideEffects(string arguments)
    {
        await using var fixture = await ShutdownCommandFixture.CreateAsync();
        var context = fixture.Context(arguments);

        await fixture.Command.ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.False(fixture.Shutdown.Requested.IsCompleted);
        Assert.Equal(0, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
        Assert.Empty(fixture.World.Sender.Sent);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentRequestsProduceOneAnnouncementAndDeadline()
    {
        await using var fixture = await ShutdownCommandFixture.CreateAsync();
        var contexts = Enumerable.Range(0, 8).Select(_ => fixture.Context("30")).ToArray();

        await Task.WhenAll(contexts.Select(fixture.Command.ExecuteAsync));

        Assert.Single(fixture.World.Sender.Sent);
        Assert.Equal(1, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
        Assert.Equal(7, contexts.Count(context => context.Output.Any(line => line.Level == CommandOutputLevel.Error)));
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(30));
        Assert.True(fixture.Shutdown.Requested.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationBeforeInvocationDoesNotScheduleOrAnnounce()
    {
        await using var fixture = await ShutdownCommandFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Command.ExecuteAsync(fixture.Context("30", cancellation.Token)));

        Assert.False(fixture.Shutdown.Requested.IsCompleted);
        Assert.Equal(0, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
        Assert.Empty(fixture.World.Sender.Sent);
    }

    [Fact]
    public async Task ExecuteAsync_TheAnnouncement_IsInTheServerLanguage()
    {
        await using var fixture = await ShutdownCommandFixture.CreateAsync();
        var broadcast = new ControlledBroadcastService { Delivery = Task.FromResult(1) };
        var command = new ShutdownCommand(
            fixture.Shutdown,
            fixture.Timers,
            broadcast,
            TestLocalization.With((30016, "Il server si sta spegnendo ora."))
        );

        await command.ExecuteAsync(fixture.Context());

        Assert.Equal("Il server si sta spegnendo ora.", Assert.Single(broadcast.Messages));
    }

    [Fact]
    public async Task ExecuteAsync_AwaitsAnnouncementBeforeSignalingHost()
    {
        await using var fixture = await ShutdownCommandFixture.CreateAsync();
        var delivery = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var broadcast = new ControlledBroadcastService { Delivery = delivery.Task };
        var command = new ShutdownCommand(fixture.Shutdown, fixture.Timers, broadcast);

        var executing = command.ExecuteAsync(fixture.Context());

        Assert.False(executing.IsCompleted);
        Assert.False(fixture.Shutdown.Requested.IsCompleted);
        delivery.SetResult(1);
        await executing;
        Assert.True(fixture.Shutdown.Requested.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_AnnouncementFailureDoesNotStopAndAllowsRetry()
    {
        await using var fixture = await ShutdownCommandFixture.CreateAsync();
        var broadcast = new ControlledBroadcastService { Delivery = Task.FromException<int>(new IOException("Send failed")) };
        var command = new ShutdownCommand(fixture.Shutdown, fixture.Timers, broadcast);

        await Assert.ThrowsAsync<IOException>(() => command.ExecuteAsync(fixture.Context()));
        Assert.False(fixture.Shutdown.Requested.IsCompleted);
        broadcast.Delivery = Task.FromResult(1);
        await command.ExecuteAsync(fixture.Context());
        Assert.True(fixture.Shutdown.Requested.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_TimerCapacityFailureAllowsRetry()
    {
        await using var fixture = await ShutdownCommandFixture.CreateAsync();
        var occupied = fixture.Timers.RegisterTimer("occupied", TimeSpan.FromSeconds(1), () => { });

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Command.ExecuteAsync(fixture.Context("30")));
        Assert.False(fixture.Shutdown.Requested.IsCompleted);
        fixture.Timers.UnregisterTimer(occupied);
        await fixture.Command.ExecuteAsync(fixture.Context("30"));
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(30));
        Assert.True(fixture.Shutdown.Requested.IsCompletedSuccessfully);
    }
}
