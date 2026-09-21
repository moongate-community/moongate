using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Moongate.Network.Server;
using Moongate.Server.Core.Data.Network;
using Moongate.Server.Services.Network;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Packets;

namespace Moongate.Tests.Integration.Network;

public sealed class NetworkServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void EmptyEndpoints_AreRejectedBeforeBinding()
        => Assert.Throws<ArgumentException>(() => new NetworkService(new NetworkListenerOptions(), new ConnectionService()));

    [Fact]
    public async Task OptionsAreSnapshotted_AndFactoryRunsPerConnection()
    {
        await using var registry = await ConnectionRegistryFixture.CreateAsync();
        var endpoint = new IPEndPoint(IPAddress.Loopback, 0);
        var endpoints = new List<IPEndPoint> { endpoint };
        var calls = 0;
        var accepted = Channel.CreateUnbounded<long>();
        var network = new NetworkService(
            new NetworkListenerOptions
            {
                Endpoints = endpoints,
                ConnectionPipelineFactory = () =>
                                            {
                                                Interlocked.Increment(ref calls);

                                                return new();
                                            }
            },
            registry.Service
        );
        endpoints.Clear();
        endpoint.Address = IPAddress.Parse("192.0.2.1");

        try
        {
            await network.StartAsync();
            network.ConnectionAccepted += (_, e) => accepted.Writer.TryWrite(e.Connection.SessionId);
            using var first = new TcpClient();
            using var second = new TcpClient();
            await first.ConnectAsync(network.Listeners[0].Endpoint);
            await second.ConnectAsync(network.Listeners[0].Endpoint);
            var firstId = await accepted.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            var secondId = await accepted.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            Assert.NotEqual(firstId, secondId);
            Assert.Equal(2, calls);
            Assert.Equal(IPAddress.Loopback, network.Listeners[0].Endpoint.Address);
        }
        finally
        {
            await network.StopAsync().WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task PartialBindFailure_ConcurrentStopJoinsRollbackAndPreservesBothFailures()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        using var middleware = new BlockingFailingCleanupMiddleware();
        var registry = new ConnectionService();
        await registry.StartAsync();
        var first = new MoongateTcpServer(
            new(IPAddress.Loopback, 0),
            connectionPipelineFactory: () => new(middlewares: [middleware])
        );
        var network = new NetworkService([first, new((IPEndPoint)occupied.LocalEndpoint)], registry);
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        network.DataReceived += (_, _) => received.TrySetResult();

        try
        {
            await first.StartAsync(default);
            using var peer = new TcpClient();
            await peer.ConnectAsync(first.Endpoint);
            await peer.GetStream().WriteAsync(new byte[] { 1 });
            await received.Task.WaitAsync(Timeout);
            var starting = network.StartAsync();
            await middleware.Entered.WaitAsync(Timeout);
            var stopping = network.StopAsync();
            Assert.False(starting.IsCompleted);
            Assert.False(stopping.IsCompleted);
            Assert.Same(stopping, network.StopAsync());
            middleware.Release();
            await Assert.ThrowsAsync<SocketException>(() => starting.WaitAsync(Timeout));
            var failure = await Assert.ThrowsAsync<AggregateException>(() => stopping.WaitAsync(Timeout));
            Assert.Contains(failure.Flatten().InnerExceptions, exception => exception is IOException);
            Assert.All(network.Listeners, listener => Assert.Equal(0, listener.Port));
            Assert.Equal(0, registry.Count);
        }
        finally
        {
            middleware.Release();
            await network.StopAsync()
                         .ConfigureAwait(
                             ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
                         );
            await registry.StopAsync()
                          .ConfigureAwait(
                              ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
                          );
        }
    }

    [Fact]
    public async Task PartialBindFailure_PreservesBindErrorWhenEarlierConnectionCleanupFails()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var registry = new ConnectionService();
        await registry.StartAsync();
        var first = new MoongateTcpServer(
            new(IPAddress.Loopback, 0),
            connectionPipelineFactory: () => new(middlewares: [new FailingCleanupMiddleware()])
        );
        var network = new NetworkService([first, new((IPEndPoint)occupied.LocalEndpoint)], registry);
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        network.ConnectionAccepted += (_, _) => accepted.TrySetResult();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        network.DataReceived += (_, _) => received.TrySetResult();

        try
        {
            await first.StartAsync(default);
            using var peer = new TcpClient();
            await peer.ConnectAsync(first.Endpoint);
            await accepted.Task.WaitAsync(Timeout);
            await peer.GetStream().WriteAsync(new byte[] { 1 });
            await received.Task.WaitAsync(Timeout);
            await Assert.ThrowsAsync<SocketException>(() => network.StartAsync().WaitAsync(Timeout));
            Assert.Equal(0, first.Port);
            Assert.Equal(0, registry.Count);
            await Assert.ThrowsAsync<AggregateException>(() => network.StopAsync().WaitAsync(Timeout));
        }
        finally
        {
            await network.StopAsync()
                         .ConfigureAwait(
                             ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
                         );
            await registry.StopAsync()
                          .ConfigureAwait(
                              ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
                          );
        }
    }

    [Fact]
    public async Task RawListener_AdmitsAndReceivesWithoutGameServices()
    {
        await using var registry = await ConnectionRegistryFixture.CreateAsync();
        var network = new NetworkService(
            new NetworkListenerOptions { Endpoints = [new(IPAddress.Loopback, 0)] },
            registry.Service
        );
        var received = Channel.CreateUnbounded<byte[]>();
        var accepted = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        network.ConnectionAccepted += (_, e) =>
                                      {
                                          Assert.True(registry.Service.TryGet(e.Connection.SessionId, out var found));
                                          Assert.Same(e.Connection, found);
                                          accepted.TrySetResult(e.Connection.SessionId);
                                      };
        network.DataReceived += (_, e) => received.Writer.TryWrite(e.Data.ToArray());

        try
        {
            await network.StartAsync();
            using var peer = new TcpClient();
            await peer.ConnectAsync(network.Listeners[0].Endpoint);
            await accepted.Task.WaitAsync(Timeout);
            Assert.Equal(1, registry.Service.Count);
            await peer.GetStream().WriteAsync(new byte[] { 0xFF, 0x00, 0xFE });
            var bytes = new List<byte>();

            while (bytes.Count < 3)
            {
                bytes.AddRange(await received.Reader.ReadAsync().AsTask().WaitAsync(Timeout));
            }

            Assert.Equal(new byte[] { 0xFF, 0x00, 0xFE }, bytes);
            peer.Close();
            await network.StopAsync().WaitAsync(Timeout);
            Assert.Equal(0, registry.Service.Count);
        }
        finally
        {
            await network.StopAsync().WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task SeparateListeners_KeepRegistriesIsolated()
    {
        await using var firstRegistry = await ConnectionRegistryFixture.CreateAsync();
        await using var secondRegistry = await ConnectionRegistryFixture.CreateAsync();
        var options = new NetworkListenerOptions { Endpoints = [new(IPAddress.Loopback, 0)] };
        var first = new NetworkService(options, firstRegistry.Service);
        var second = new NetworkService(options, secondRegistry.Service);
        var accepted = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        second.ConnectionAccepted += (_, e) => accepted.TrySetResult(e.Connection.SessionId);

        try
        {
            await first.StartAsync();
            await second.StartAsync();
            using var peer = new TcpClient();
            await peer.ConnectAsync(second.Listeners[0].Endpoint);
            var id = await accepted.Task.WaitAsync(Timeout);
            Assert.False(firstRegistry.Service.TryGet(id, out _));
            await first.StopAsync();
            Assert.True(secondRegistry.Service.TryGet(id, out _));
        }
        finally
        {
            await Task.WhenAll(first.StopAsync(), second.StopAsync()).WaitAsync(Timeout);
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task ThrowingSubscriber_ClosesOffenderAndStillPublishesClosure(bool onAccept)
    {
        await using var registry = await ConnectionRegistryFixture.CreateAsync();
        var network = new NetworkService(
            new NetworkListenerOptions { Endpoints = [new(IPAddress.Loopback, 0)] },
            registry.Service
        );
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (onAccept)
        {
            network.ConnectionAccepted += (_, _) => throw new IOException("accept callback");
        }
        else
        {
            network.DataReceived += (_, _) => throw new IOException("data callback");
        }

        network.ConnectionClosed += (_, _) => throw new IOException("first close callback");
        network.ConnectionClosed += (_, _) => closed.TrySetResult();

        try
        {
            await network.StartAsync();
            using var peer = new TcpClient();
            await peer.ConnectAsync(network.Listeners[0].Endpoint);

            if (!onAccept)
            {
                await peer.GetStream().WriteAsync(new byte[] { 1 });
            }

            await closed.Task.WaitAsync(Timeout);
            await network.StopAsync().WaitAsync(Timeout);
            Assert.Equal(0, registry.Service.Count);
        }
        finally
        {
            await network.StopAsync().WaitAsync(Timeout);
        }
    }
}
