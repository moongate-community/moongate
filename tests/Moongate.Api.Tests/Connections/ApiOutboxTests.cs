using Microsoft.Extensions.Time.Testing;
using Moongate.Api.Connections.Internal;
using Moongate.Api.Tests.TestSupport.Connections;

namespace Moongate.Api.Tests.Connections;

public class ApiOutboxTests
{
    [Fact]
    public async Task CancelQueuedRequest_ReclaimsCapacityAndPreservesWireOrder()
    {
        var transport = new RecordingConnection { SendGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var outbox = new ApiOutbox(transport, 2, TimeSpan.FromSeconds(5), TimeProvider.System);
        Assert.True(outbox.TryEnqueue(new([1], 1)));
        await transport.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(outbox.TryEnqueue(new([2], 2)));
        Assert.True(outbox.TryEnqueue(new([3], null)));
        Assert.False(outbox.TryEnqueue(new([4], 4)));
        Assert.False(outbox.TryRemove(1));
        Assert.True(outbox.TryRemove(2));
        Assert.False(outbox.TryRemove(3));
        Assert.True(outbox.TryEnqueue(new([4], 4)));
        outbox.Complete();
        transport.SendGate.SetResult();
        await outbox.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new byte[] { 1, 3, 4 }, transport.Sent.Select(frame => frame[0]));
    }

    [Fact]
    public async Task StalledWrite_TimesOutAndClosesTransport()
    {
        var clock = new FakeTimeProvider();
        var transport = new RecordingConnection { SendGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var outbox = new ApiOutbox(transport, 2, TimeSpan.FromSeconds(5), clock);
        Assert.True(outbox.TryEnqueue(new([1], 1)));
        await transport.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<TimeoutException>(() => outbox.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(transport.IsConnected);
        Assert.False(outbox.TryEnqueue(new([2], 2)));
    }
}
