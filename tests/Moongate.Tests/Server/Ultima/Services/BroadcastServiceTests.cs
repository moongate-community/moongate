using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class BroadcastServiceTests
{
    [Fact]
    public async Task BroadcastAsync_CompressedPayloadOverflowRejectsBeforeQueueingToAnyPlayer()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(1);
        await fixture.AddAsync(2);
        var service = new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender);

        await Assert.ThrowsAsync<ArgumentException>(() => service.BroadcastAsync(new string('\uAFAF', 30000)));

        Assert.Empty(fixture.Sender.Sent);
    }

    [Fact]
    public async Task BroadcastAsync_LargeMessageThatFitsCompressionIsDeliveredIntact()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(1);
        var service = new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender);
        var text = new string('A', 30000);

        Assert.Equal(1, await service.BroadcastAsync(text));
        Assert.Equal(text, Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.Sender.Sent)).Text);
    }

    [Fact]
    public async Task BroadcastAsync_OnlyConnectedCharactersInWorldReceiveSystemTextAcrossMaps()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(1);
        await fixture.AddAsync(2, map: MapType.Felucca);
        await fixture.AddAsync(3, entered: false);
        await fixture.AddAsync(4, connected: false);
        var service = new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender);
        fixture.Sender.OnSent = _ => Assert.True(fixture.Network.Loop.IsOnLoopThread);

        var sent = await service.BroadcastAsync("Hello, Britannia! è 世界");

        Assert.Equal(2, sent);
        Assert.Equal(new long[] { 1, 2 }, fixture.Sender.SentSessionIds.Order());
        Assert.All(
            fixture.Sender.Sent,
            message =>
            {
                var packet = Assert.IsType<UnicodeSpeechMessagePacket>(message);
                Assert.Equal("Hello, Britannia! è 世界", packet.Text);
                Assert.Equal(SpeechType.System, packet.Type);
                Assert.Equal("System", packet.Name);
            }
        );
    }

    [Fact]
    public async Task BroadcastAsync_NoPlayersReturnsZero()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var service = new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender);

        Assert.Equal(0, await service.BroadcastAsync("Hello"));
        Assert.Empty(fixture.Sender.Sent);
    }

    [Fact]
    public async Task BroadcastAsync_CanceledBeforeAdmissionSendsNothing()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(1);
        var service = new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.BroadcastAsync("Hello", cancellation.Token));
        Assert.Empty(fixture.Sender.Sent);
    }

    [Fact]
    public async Task BroadcastAsync_OnLoopSendsWithoutPostingToOwnInbox()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(1);
        var service = new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender);
        Task<int>? sending = null;

        await fixture.Network.ExecuteOnLoopAsync(() => sending = service.BroadcastAsync("Hello"));

        Assert.NotNull(sending);
        Assert.Equal(1, await sending);
    }
}
