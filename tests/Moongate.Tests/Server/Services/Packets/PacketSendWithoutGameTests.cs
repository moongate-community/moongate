using Moongate.Network.Packets.General;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Packets;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Server.Services.Packets;

public sealed class PacketSendWithoutGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task DisconnectWithoutOutbox_ClosesConnectionAndStopOwnsPendingCleanup()
    {
        var connections = new ConnectionService();
        await connections.StartAsync();
        using var connection = new ControlledNetworkConnection(1) { DelayCompletion = true };
        connections.TryRegister(connection);
        var sender = new PacketSendService(connections);
        await sender.StartAsync();
        var closing = sender.DisconnectAsync(1);
        var stopping = sender.StopAsync();

        try
        {
            Assert.False(closing.IsCompleted);
            Assert.False(stopping.IsCompleted);
            await connection.CloseRequested.WaitAsync(Timeout);
        }
        finally
        {
            connection.Complete();
        }

        await Task.WhenAll(closing, stopping).WaitAsync(Timeout);
        await connections.StopAsync();
    }

    [Fact]
    public async Task FullQueue_ClosePending_RejectsFurtherPacketsAndOwnsDrain()
    {
        var sendGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connections = new ConnectionService();
        await connections.StartAsync();
        using var connection = new ControlledNetworkConnection(1)
        {
            SendGate = sendGate.Task,
            DelayCompletion = true,
            DelayDisconnectionState = true
        };
        connections.TryRegister(connection);
        var sender = new PacketSendService(connections, 1);
        await sender.StartAsync();

        try
        {
            Assert.True(sender.TrySend(1, new PingPacket(1)));
            await connection.SendStarted.WaitAsync(Timeout);
            Assert.True(sender.TrySend(1, new PingPacket(2)));
            Assert.False(sender.TrySend(1, new PingPacket(3)));
            Assert.True(connection.IsConnected);
            Assert.False(connections.TryGet(1, out _));
            Assert.False(sender.TrySend(1, new PingPacket(4)));
            Assert.False(sender.DisconnectAsync(1).IsCompleted);
        }
        finally
        {
            sendGate.TrySetResult();
            connection.Complete();
            await sender.StopAsync().WaitAsync(Timeout);
            await connections.StopAsync().WaitAsync(Timeout);
        }

        Assert.Equal(0, sender.ActiveOutboxCount);
        Assert.Equal(0, connections.Count);
    }

    [Fact]
    public async Task RequestedClose_InterruptsActiveSocketWriteWithoutReportingASendFailure()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registry = await ConnectionRegistryFixture.CreateAsync();
        using var connection = new ControlledNetworkConnection(17)
        {
            SendGate = release.Task,
            SendFailure = new IOException("write interrupted by local close")
        };
        registry.Service.TryRegister(connection);
        var sender = new PacketSendService(registry.Service);
        await sender.StartAsync();

        try
        {
            Assert.True(sender.TrySend(17, new PingPacket(1)));
            await connection.SendStarted.WaitAsync(Timeout);
            var stopping = sender.StopAsync();
            await connection.CloseRequested.WaitAsync(Timeout);
            Assert.False(stopping.IsCompleted);
            release.TrySetResult();
            await stopping.WaitAsync(Timeout);
            Assert.Equal(0, sender.ActiveOutboxCount);
        }
        finally
        {
            release.TrySetResult();
            await sender.StopAsync()
                        .ConfigureAwait(
                            ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
                        );
        }
    }

    [Fact]
    public async Task SendFailure_ClosesTransportAndRemainsObservableAtStop()
    {
        var connections = new ConnectionService();
        await connections.StartAsync();
        using var connection = new ControlledNetworkConnection(1) { SendFailure = new IOException("write failed") };
        connections.TryRegister(connection);
        var sender = new PacketSendService(connections);
        await sender.StartAsync();
        Assert.True(sender.TrySend(1, new PingPacket(1)));
        await connection.CloseRequested.WaitAsync(Timeout);
        await Assert.ThrowsAsync<AggregateException>(() => sender.StopAsync().WaitAsync(Timeout));
        Assert.False(sender.TrySend(1, new PingPacket(2)));
        await connections.StopAsync();
    }

    [Fact]
    public async Task TrySend_OnlyConnectionRegistry_ProducesExactPacketBytes()
    {
        var connections = new ConnectionService();
        await connections.StartAsync();
        using var connection = new ControlledNetworkConnection(1);
        connections.TryRegister(connection);
        var sender = new PacketSendService(connections);
        await sender.StartAsync();

        try
        {
            Assert.True(sender.TrySend(1, new PingPacket(0x2A)));
            using var deadline = new CancellationTokenSource(Timeout);
            Assert.Equal(new byte[] { 0x73, 0x2A }, await connection.ReadSentAsync(deadline.Token));
        }
        finally
        {
            await sender.StopAsync().WaitAsync(Timeout);
            await connections.StopAsync().WaitAsync(Timeout);
        }
    }
}
