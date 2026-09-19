using Moongate.Server.Services.Network;
using Moongate.Server.Services.Packets.Internal;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Server.Services.Packets.Internal;

public sealed class SessionPacketOutboxTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Theory, InlineData(false), InlineData(true)]
    public async Task TransportCompletesBeforeSendContinuation_PreservesOriginalFailure(bool canceled)
    {
        await using var registry = await ConnectionRegistryFixture.CreateAsync();
        Exception failure = canceled ? new OperationCanceledException("independent cancellation") : new IOException("send failure");
        var releaseFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var connection = new ControlledNetworkConnection(17)
        {
            SendFailure = failure,
            CompleteOnSendFailure = true,
            // No I/O remains, but delivery of the failed send result to the consumer is held back.
            SendFailureDeliveryGate = releaseFailure.Task
        };
        registry.Service.TryRegister(connection);
        Assert.True(registry.Service.TryGet(17, out _, out var requested));
        const int capacity = 1_000_000;
        var outbox = new SessionPacketOutbox(connection, requested, capacity, registry.Service.DisconnectAsync);
        outbox.Start();
        Assert.True(outbox.TryWrite(new byte[] { 0x73, 1 }));
        try
        {
            await connection.Completion.WaitAsync(Timeout);
            using var deadline = new CancellationTokenSource(Timeout);
            var queued = 0;
            while (outbox.TryWrite(Array.Empty<byte>()))
            {
                // Observe queue admission closing without filling it or accessing private state.
                Assert.True(++queued < capacity);
                await Task.Delay(1, deadline.Token);
            }
            Assert.False(outbox.Completion.IsCompleted);
            Assert.False(releaseFailure.Task.IsCompleted);
            releaseFailure.TrySetResult();
            var error = await Assert.ThrowsAsync<AggregateException>(() => outbox.Completion.WaitAsync(Timeout));
            Assert.Contains(failure, error.Flatten().InnerExceptions);
            Assert.False(requested.IsCompleted);
        }
        finally
        {
            releaseFailure.TrySetResult();
            await outbox.Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        }
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task RegistryRequestedClose_InterruptedWriteCompletesWithoutSendFailure(bool canceled)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outboxClosure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connections = new ConnectionService();
        await connections.StartAsync();
        using var connection = new ControlledNetworkConnection(77)
        {
            SendGate = release.Task,
            SendFailure = canceled ? new OperationCanceledException("requested close") : new IOException("requested close"),
            DelayCompletion = true
        };
        Assert.True(connections.TryRegister(connection));
        Assert.True(connections.TryGet(77, out _, out var disconnectRequested));
        var outbox = new SessionPacketOutbox(connection, disconnectRequested, 1, id =>
        {
            outboxClosure.TrySetResult();
            return connections.DisconnectAsync(id);
        });
        outbox.Start();
        Assert.True(outbox.TryWrite(new byte[] { 0x73, 1 }));
        try
        {
            await connection.SendStarted.WaitAsync(Timeout);
            var closing = connections.DisconnectAsync(connection.SessionId);
            await connection.CloseRequested.WaitAsync(Timeout);
            release.TrySetResult();
            await outboxClosure.Task.WaitAsync(Timeout);
            connection.Complete();
            await closing.WaitAsync(Timeout);
            await outbox.Completion.WaitAsync(Timeout);
        }
        finally
        {
            release.TrySetResult();
            connection.Complete();
            await connections.StopAsync().WaitAsync(Timeout);
        }
    }
}
