using Microsoft.Extensions.Time.Testing;
using Moongate.Api.Connections.Internal;
using Moongate.Api.Data.Config;
using Moongate.Api.Exceptions;
using Moongate.Api.Registry;
using Moongate.Api.Serialization.Internal;
using Moongate.Api.Tests.TestSupport.Connections;
using Moongate.Api.Tests.TestSupport.Contracts;
namespace Moongate.Api.Tests.Connections;
public class ApiPendingCallsTests
{
    [Fact]
    public async Task CapacityFailure_IsBeforeSendAndDisconnectFailsAllReservations()
    {
        var transport = new RecordingConnection();
        var outbox = new ApiOutbox(transport, 32, TimeSpan.FromSeconds(5), TimeProvider.System);
        var pending = new ApiPendingCalls(outbox, new ApiOptions(), TimeProvider.System);
        var operation = Registry().Get<IncrementRequest, IncrementResponse>();
        var calls = Enumerable.Range(0, 16).Select(_ => pending.RequestAsync(operation, new IncrementRequest(), null, default)).ToArray();
        Assert.Equal(16, pending.Count);
        await Assert.ThrowsAsync<ApiBusyException>(() => pending.RequestAsync(operation, new IncrementRequest(), null, default));
        pending.FailAll(new IOException("Disconnected."));
        foreach (var call in calls) { await Assert.ThrowsAsync<IOException>(() => call); }
        Assert.Equal(0, pending.Count);
        outbox.Complete();
        await outbox.Completion;
    }

    [Fact]
    public async Task Response_CompletesOnceAndRejectsNeverAssignedOrWrongOperation()
    {
        var transport = new RecordingConnection();
        var outbox = new ApiOutbox(transport, 2, TimeSpan.FromSeconds(5), TimeProvider.System);
        var pending = new ApiPendingCalls(outbox, new ApiOptions(), TimeProvider.System);
        var call = pending.RequestAsync(Registry().Get<IncrementRequest, IncrementResponse>(), new IncrementRequest(), null, default);
        var response = ApiPayloadSerializer.Serialize(new IncrementResponse { Value = 42 }, 100);
        Assert.Throws<ApiProtocolException>(() => pending.TryComplete(2, 100, response, null));
        Assert.Throws<ApiProtocolException>(() => pending.TryComplete(1, 101, response, null));
        Assert.True(pending.TryComplete(1, 100, response, null));
        Assert.Equal(42, Assert.IsType<IncrementResponse>(await call).Value);
        Assert.False(pending.TryComplete(1, 100, response, null));
        outbox.Complete();
        await outbox.Completion;
    }

    [Fact]
    public async Task LocalTimeout_ReclaimsQueuedRequestBeforeWriterUnblocks()
    {
        var clock = new FakeTimeProvider();
        var transport = new RecordingConnection { SendGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var outbox = new ApiOutbox(transport, 1, TimeSpan.FromMinutes(1), clock);
        var pending = new ApiPendingCalls(outbox, new ApiOptions(), clock);
        var operation = Registry().Get<IncrementRequest, IncrementResponse>();
        var first = pending.RequestAsync(operation, new IncrementRequest(), TimeSpan.FromMinutes(1), default);
        await transport.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var expired = pending.RequestAsync(operation, new IncrementRequest(), null, default);
        clock.Advance(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<TimeoutException>(() => expired);
        using var cancellation = new CancellationTokenSource();
        var replacement = pending.RequestAsync(operation, new IncrementRequest(), null, cancellation.Token);
        Assert.Equal(3u, pending.LastAssignedId);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => replacement);
        pending.FailAll(new IOException("Disconnected."));
        await Assert.ThrowsAsync<IOException>(() => first);
        transport.SendGate.SetResult();
        outbox.Complete();
        await outbox.Completion;
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task IdentifierExhaustion_DoesNotWrapOrMutateCounter()
    {
        var outbox = new ApiOutbox(new RecordingConnection(), 1, TimeSpan.FromSeconds(5), TimeProvider.System);
        var pending = new ApiPendingCalls(outbox, new ApiOptions(), TimeProvider.System, uint.MaxValue - 1);
        Assert.Equal(uint.MaxValue, pending.ReserveNextId());
        Assert.Throws<InvalidOperationException>(() => pending.ReserveNextId());
        Assert.Equal(uint.MaxValue, pending.LastAssignedId);
        outbox.Complete();
        await outbox.Completion;
    }

    [Fact]
    public async Task SerializationFailure_ReleasesReservation()
    {
        var outbox = new ApiOutbox(new RecordingConnection(), 1, TimeSpan.FromSeconds(5), TimeProvider.System);
        var pending = new ApiPendingCalls(outbox, new ApiOptions(), TimeProvider.System);
        await Assert.ThrowsAsync<InvalidCastException>(() => pending.RequestAsync(Registry().Get<IncrementRequest, IncrementResponse>(), new object(), null, default));
        Assert.Equal(0, pending.Count);
        Assert.Equal(0u, pending.LastAssignedId);
        outbox.Complete();
        await outbox.Completion;
    }

    [Fact]
    public async Task ConcurrentAdmission_AssignsIdentifiersInWireOrder()
    {
        var transport = new RecordingConnection { SendGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var outbox = new ApiOutbox(transport, 128, TimeSpan.FromSeconds(5), TimeProvider.System);
        var pending = new ApiPendingCalls(outbox, new ApiOptions { MaxPendingCalls = 128 }, TimeProvider.System);
        var operation = Registry().Get<IncrementRequest, IncrementResponse>();
        var calls = new System.Collections.Concurrent.ConcurrentBag<Task<object>>();
        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => { calls.Add(pending.RequestAsync(operation, new IncrementRequest(), null, default)); })));
        outbox.Complete();
        transport.SendGate.SetResult();
        await outbox.Completion;
        foreach (var call in calls) { await Assert.ThrowsAsync<IOException>(() => call); }
        var codec = new ApiFrameCodec(65536);
        Assert.Equal(Enumerable.Range(1, 100).Select(id => (uint)id), transport.Sent.Select(frame => codec.Decode(frame).RequestId));
    }

    [Fact]
    public async Task ResponseAndCancellationRace_LeavesNoReservations()
    {
        var outbox = new ApiOutbox(new RecordingConnection(), 128, TimeSpan.FromSeconds(5), TimeProvider.System);
        var pending = new ApiPendingCalls(outbox, new ApiOptions(), TimeProvider.System);
        var operation = Registry().Get<IncrementRequest, IncrementResponse>();
        var response = ApiPayloadSerializer.Serialize(new IncrementResponse { Value = 42 }, 100);
        for (var index = 0; index < 100; index++)
        {
            using var cancellation = new CancellationTokenSource();
            var call = pending.RequestAsync(operation, new IncrementRequest(), null, cancellation.Token);
            var id = pending.LastAssignedId;
            await Task.WhenAll(Task.Run(cancellation.Cancel), Task.Run(() => pending.TryComplete(id, 100, response, null)));
            try { Assert.Equal(42, Assert.IsType<IncrementResponse>(await call).Value); }
            catch (OperationCanceledException) { Assert.True(cancellation.IsCancellationRequested); }
            Assert.Equal(0, pending.Count);
        }
        outbox.Complete();
        await outbox.Completion;
    }

    private static ApiRegistry Registry()
    {
        var registry = new ApiRegistry();
        registry.RegisterContract<IncrementRequest, IncrementResponse>();
        registry.Freeze();
        return registry;
    }
}
