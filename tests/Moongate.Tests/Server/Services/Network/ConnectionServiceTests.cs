using Moongate.Server.Services.Network;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Server.Services.Network;

public sealed class ConnectionServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Theory, InlineData(false), InlineData(true)]
    public async Task TryGet_DisconnectSignalSurvivesMembershipRemoval(bool stop)
    {
        var service = new ConnectionService();
        await service.StartAsync();
        using var connection = new ControlledNetworkConnection(1);
        service.TryRegister(connection);
        Assert.True(service.TryGet(1, out var found, out var requested));
        Assert.Same(connection, found);
        Assert.False(requested.IsCompleted);
        if (stop)
        {
            await service.StopAsync().WaitAsync(Timeout);
        }
        else
        {
            await service.DisconnectAsync(1).WaitAsync(Timeout);
        }

        Assert.Equal(0, service.Count);
        Assert.True(requested.IsCompletedSuccessfully);
        await service.StopAsync();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task RemoteCompletion_DoesNotBecomeAnOwnerRequestedClose(bool redundantDisconnect)
    {
        var service = new ConnectionService();
        await service.StartAsync();
        using var connection = new ControlledNetworkConnection(1);
        service.TryRegister(connection);
        Assert.True(service.TryGet(1, out _, out var requested));
        connection.Complete();
        if (redundantDisconnect)
        {
            await service.DisconnectAsync(1).WaitAsync(Timeout);
        }

        await service.StopAsync().WaitAsync(Timeout);
        Assert.False(requested.IsCompleted);
    }

    [Fact]
    public async Task TryRegister_RequiresRunningAndLiveConnection()
    {
        var service = new ConnectionService();
        using var connection = new ControlledNetworkConnection(1);
        Assert.False(service.TryRegister(connection));
        await service.StartAsync();
        connection.Complete();
        Assert.False(service.TryRegister(connection));
        await service.StopAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync());
    }

    [Fact]
    public async Task TryRegister_DuplicateIdentityCannotReplaceTheOriginal()
    {
        var service = new ConnectionService();
        await service.StartAsync();
        using var first = new ControlledNetworkConnection(1);
        using var conflicting = new ControlledNetworkConnection(1);
        using var second = new ControlledNetworkConnection(2);
        Assert.True(service.TryRegister(first));
        var snapshot = service.GetAll();
        Assert.True(service.TryRegister(first));
        Assert.False(service.TryRegister(conflicting));
        Assert.True(service.TryRegister(second));
        Assert.Same(first, Assert.Single(snapshot));
        Assert.Equal(2, service.Count);
        Assert.True(service.TryGet(1, out var found));
        Assert.Same(first, found);
        await service.StopAsync().WaitAsync(Timeout);
        Assert.True(conflicting.IsConnected);
        Assert.Equal(0, conflicting.CloseCalls);
    }

    [Fact]
    public async Task DisconnectAsync_RemovesAdmissionBeforeActualCompletion()
    {
        var service = new ConnectionService();
        await service.StartAsync();
        using var connection = new ControlledNetworkConnection(1) { DelayCompletion = true, DelayDisconnectionState = true };
        Assert.True(service.TryRegister(connection));
        var closing = service.DisconnectAsync(1);
        try
        {
            Assert.True(connection.IsConnected);
            Assert.False(service.TryGet(1, out _));
            Assert.False(closing.IsCompleted);
            Assert.Equal(1, service.Count);
            Assert.Same(closing, service.DisconnectAsync(1));
            await connection.CloseRequested.WaitAsync(Timeout);
            Assert.Equal(1, connection.CloseCalls);
        }
        finally
        {
            connection.Complete();
        }

        await closing.WaitAsync(Timeout);
        Assert.Equal(0, service.Count);
        await service.StopAsync();
    }

    [Fact]
    public async Task DisconnectAsync_TransportCompletesFirst_StillOwnsCloseRequest()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new ConnectionService();
        await service.StartAsync();
        using var connection = new ControlledNetworkConnection(1) { CloseGate = gate.Task };
        service.TryRegister(connection);
        var closing = service.DisconnectAsync(1);
        try
        {
            await connection.CloseRequested.WaitAsync(Timeout);
            connection.Complete();
            Assert.False(closing.IsCompleted);
            Assert.Equal(1, service.Count);
        }
        finally
        {
            gate.TrySetResult();
        }

        await closing.WaitAsync(Timeout);
        Assert.Equal(0, service.Count);
        await service.StopAsync();
    }

    [Fact]
    public async Task RemoteCompletion_RetiresMembershipWithoutDisconnectCallback()
    {
        var service = new ConnectionService();
        await service.StartAsync();
        using var connection = new ControlledNetworkConnection(1);
        service.TryRegister(connection);
        connection.Complete();
        await connection.CloseRequested.WaitAsync(Timeout);
        await service.StopAsync().WaitAsync(Timeout);
        Assert.Equal(0, service.Count);
        Assert.False(service.TryGet(1, out _));
    }

    [Fact]
    public async Task SeparateRegistries_CanOwnTheSameIdWithoutCrossClosing()
    {
        var first = new ConnectionService();
        var second = new ConnectionService();
        await first.StartAsync();
        await second.StartAsync();
        using var a = new ControlledNetworkConnection(1);
        using var b = new ControlledNetworkConnection(1);
        first.TryRegister(a);
        second.TryRegister(b);
        await first.StopAsync().WaitAsync(Timeout);
        Assert.True(second.TryGet(1, out var found));
        Assert.Same(b, found);
        Assert.True(b.IsConnected);
        await second.StopAsync();
    }

    [Fact]
    public async Task StopAsync_ClosesEveryConnectionAndPreservesCleanupFailure()
    {
        var service = new ConnectionService();
        await service.StartAsync();
        using var faulty = new ControlledNetworkConnection(1) { DelayCompletion = true };
        using var healthy = new ControlledNetworkConnection(2);
        service.TryRegister(faulty);
        service.TryRegister(healthy);
        var stopping = service.StopAsync();
        Assert.Same(stopping, service.StopAsync());
        using var late = new ControlledNetworkConnection(3);
        Assert.False(service.TryRegister(late));
        await Task.WhenAll(faulty.CloseRequested, healthy.CloseRequested).WaitAsync(Timeout);
        faulty.Complete(new IOException("cleanup failed"));
        var error = await Assert.ThrowsAsync<AggregateException>(() => stopping.WaitAsync(Timeout));
        Assert.Contains(error.Flatten().InnerExceptions, exception => exception is IOException);
        Assert.Equal(0, service.Count);
        Assert.False(healthy.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_CloseFailureIsObservedAndRemainsVisibleAtStop()
    {
        var service = new ConnectionService();
        await service.StartAsync();
        using var connection = new ControlledNetworkConnection(1)
        {
            DelayCompletion = true,
            CloseFailure = new IOException("close failed")
        };
        service.TryRegister(connection);
        var closing = service.DisconnectAsync(1);
        await connection.CloseRequested.WaitAsync(Timeout);
        connection.Complete();
        await Assert.ThrowsAsync<AggregateException>(() => closing.WaitAsync(Timeout));
        await Assert.ThrowsAsync<AggregateException>(() => service.StopAsync().WaitAsync(Timeout));
        Assert.Equal(0, service.Count);
    }

    [Fact]
    public async Task StopBeforeStart_RejectsLaterStartupAndUnknownDisconnectIsNoOp()
    {
        var service = new ConnectionService();
        await service.StopAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync());
        await service.DisconnectAsync(42);
        Assert.Empty(service.GetAll());
    }
}
