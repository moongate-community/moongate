using System.Net;
using System.Net.Sockets;
using Moongate.Network.Data;
using Moongate.Network.Data.Events;
using Moongate.Network.Server;
using Moongate.Network.Tests.Support;

namespace Moongate.Network.Tests.Integration.Server;

public sealed class MoongateTcpServerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(0, 1, "receiveBufferSize")]
    [InlineData(1024 * 1024 + 1, 1, "receiveBufferSize")]
    [InlineData(1, 0, "maxFrameLength")]
    [InlineData(1, 16 * 1024 * 1024 + 1, "maxFrameLength")]
    public void Constructor_OutOfRangeBufferLimits_RejectsBeforeBind(
        int receiveBufferSize,
        int maxFrameLength,
        string parameterName
    )
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new MoongateTcpServer(
            new IPEndPoint(IPAddress.Loopback, 0),
            receiveBufferSize: receiveBufferSize,
            maxFrameLength: maxFrameLength
        ));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1024 * 1024, 16 * 1024 * 1024)]
    public void Constructor_ExactBufferLimitBoundaries_AcceptsConfiguration(
        int receiveBufferSize,
        int maxFrameLength
    )
    {
        using var server = new MoongateTcpServer(
            new IPEndPoint(IPAddress.Loopback, 0),
            receiveBufferSize: receiveBufferSize,
            maxFrameLength: maxFrameLength
        );

        Assert.Equal(0, server.Port);
    }

    [Fact]
    public async Task StartAsync_LoopbackClient_DeliversACompleteFrame()
    {
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new MoongateTcpServer(
            new IPEndPoint(IPAddress.Loopback, 0), framer: new LengthPrefixFramer());
        server.OnDataReceived += (_, args) => received.TrySetResult(args.Data.ToArray());
        await server.StartAsync(CancellationToken.None);
        Assert.True(server.IsRunning);
        Assert.InRange(server.Port, 1, 65535);

        using var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, server.Port);
        await peer.GetStream().WriteAsync(new byte[] { 1, 0x42 });

        Assert.Equal(new byte[] { 1, 0x42 }, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task StartAsync_BindFails_ReleasesStateAndCanRetry()
    {
        var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var endpoint = (IPEndPoint)occupied.LocalEndpoint;
        await using var server = new MoongateTcpServer(endpoint);
        try
        {
            await Assert.ThrowsAsync<SocketException>(() => server.StartAsync(CancellationToken.None));
            Assert.False(server.IsRunning);
            Assert.Equal(0, server.Port);
        }
        finally
        {
            occupied.Stop();
        }
        await server.StartAsync(CancellationToken.None).WaitAsync(Timeout);
        Assert.True(server.IsRunning);
        Assert.Equal(endpoint.Port, server.Port);
    }

    [Fact]
    public async Task StartAsync_PreCanceled_DoesNotBindAndCanRetry()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.StartAsync(cancellation.Token));
        Assert.False(server.IsRunning);
        Assert.Equal(0, server.Port);
        await server.StartAsync(CancellationToken.None).WaitAsync(Timeout);
        Assert.True(server.IsRunning);
    }

    [Fact]
    public async Task StartAsync_ConcurrentCalls_ShareOneListener()
    {
        await using var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));
        var starts = Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
        {
            await server.StartAsync(CancellationToken.None);
            return server.Port;
        })).ToArray();
        var ports = await Task.WhenAll(starts).WaitAsync(Timeout);
        Assert.InRange(ports[0], 1, 65535);
        Assert.All(ports, port => Assert.Equal(ports[0], port));
    }

    [Fact]
    public async Task StopAsync_ConcurrentCalls_AllWaitForCleanup()
    {
        using var handler = new BlockingConnectHandler();
        await using var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));
        server.OnClientConnect += handler.Handle;
        await server.StartAsync(CancellationToken.None);
        using var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(Timeout);
        await handler.Entered.Task.WaitAsync(Timeout);
        var stops = Enumerable.Range(0, 16).Select(_ => server.StopAsync(CancellationToken.None)).ToArray();
        try
        {
            Assert.All(stops, stop => Assert.False(stop.IsCompleted));
        }
        finally
        {
            handler.Release.Set();
            await Task.WhenAll(stops).WaitAsync(Timeout);
        }
        Assert.Equal(0, server.Port);
        Assert.Equal(0, await peer.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(Timeout));
    }

    [Fact]
    public async Task StartAsync_DuringStop_WaitsForPreviousGeneration()
    {
        using var handler = new BlockingConnectHandler();
        await using var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));
        server.OnClientConnect += handler.Handle;
        await server.StartAsync(CancellationToken.None);
        using var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(Timeout);
        await handler.Entered.Task.WaitAsync(Timeout);
        var stop = server.StopAsync(CancellationToken.None);
        var restart = server.StartAsync(CancellationToken.None);
        try
        {
            Assert.False(restart.IsCompleted);
        }
        finally
        {
            handler.Release.Set();
            await stop.WaitAsync(Timeout);
            await restart.WaitAsync(Timeout);
        }
        Assert.True(server.IsRunning);
        Assert.Equal(0, await peer.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(Timeout));
    }

    [Fact]
    public async Task StopAsync_CanceledWaiter_CleanupCanStillBeAwaited()
    {
        using var handler = new BlockingConnectHandler();
        await using var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));
        server.OnClientConnect += handler.Handle;
        await server.StartAsync(CancellationToken.None);
        using var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(Timeout);
        await handler.Entered.Task.WaitAsync(Timeout);
        using var waiter = new CancellationTokenSource();
        var stop = server.StopAsync(waiter.Token);
        try
        {
            waiter.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stop.WaitAsync(Timeout));
        }
        finally
        {
            handler.Release.Set();
            await server.StopAsync(CancellationToken.None).WaitAsync(Timeout);
        }
        Assert.Equal(0, server.Port);
        Assert.Equal(0, await peer.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(Timeout));
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task AcceptLoop_FailedConnectionAndDiagnosticHandler_ContinuesAccepting(bool failFactory)
    {
        var attempts = 0;
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new MoongateTcpServer(new(IPAddress.Loopback, 0),
            connectionPipelineFactory: () => failFactory && Interlocked.Increment(ref attempts) == 1
                ? throw new SocketException((int)SocketError.InvalidArgument)
                : new ConnectionPipeline());
        server.OnException += (_, _) => throw new InvalidOperationException("diagnostic failure");
        server.OnException += (_, _) => failed.TrySetResult();
        server.OnClientConnect += (_, _) =>
        {
            if (!failFactory && Interlocked.Increment(ref attempts) == 1)
            {
                throw new InvalidOperationException("connect failure");
            }
            connected.TrySetResult();
        };
        await server.StartAsync(CancellationToken.None);
        using var first = new TcpClient();
        await first.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(Timeout);
        await failed.Task.WaitAsync(Timeout);
        Assert.Equal(0, await first.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(Timeout));
        using var second = new TcpClient();
        await second.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(Timeout);
        await connected.Task.WaitAsync(Timeout);
        Assert.True(server.IsRunning);
    }

    [Fact]
    public async Task StartAsync_LifetimeCanceled_StopsGeneration()
    {
        using var lifetime = new CancellationTokenSource();
        await using var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));
        await server.StartAsync(lifetime.Token);
        lifetime.Cancel();
        Assert.False(server.IsRunning);
        await server.StopAsync(CancellationToken.None).WaitAsync(Timeout);
        Assert.False(server.IsRunning);
        Assert.Equal(0, server.Port);
        await server.StartAsync(CancellationToken.None).WaitAsync(Timeout);
        Assert.True(server.IsRunning);
    }

    [Fact]
    public async Task DisposeAsync_IsTerminal()
    {
        var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));
        await server.DisposeAsync().AsTask().WaitAsync(Timeout);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => server.StartAsync(CancellationToken.None));
        Assert.Equal(0, server.Port);
    }

    [Fact]
    public async Task StartStop_TwentyFiveGenerations_DeliverFramesAndClosePeers()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var server = new MoongateTcpServer(new(IPAddress.Loopback, 0), framer: new LengthPrefixFramer());
        try
        {
            var configuredEndpoint = server.Endpoint;
            Assert.Equal(new IPEndPoint(IPAddress.Loopback, 0), configuredEndpoint);
            configuredEndpoint.Address = IPAddress.None;
            configuredEndpoint.Port = 1;

            for (var generation = 0; generation < 25; generation++)
            {
                var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler<TcpDataReceivedEventArgs> handler =
                    (_, args) => received.TrySetResult(args.Data.ToArray());
                server.OnDataReceived += handler;
                var peer = new TcpClient();
                try
                {
                    await server.StartAsync(deadline.Token);
                    var listeningEndpoint = server.Endpoint;
                    Assert.Equal(server.Port, listeningEndpoint.Port);
                    await peer.ConnectAsync(listeningEndpoint, deadline.Token);
                    var frame = new byte[] { 1, (byte)generation };
                    await peer.GetStream().WriteAsync(frame, deadline.Token);
                    Assert.Equal(frame, await received.Task.WaitAsync(deadline.Token));
                    await server.StopAsync(CancellationToken.None).WaitAsync(deadline.Token);
                    Assert.Equal(0, server.Port);
                    Assert.Equal(new IPEndPoint(IPAddress.Loopback, 0), server.Endpoint);
                    Assert.InRange(listeningEndpoint.Port, 1, 65535);
                    Assert.Equal(0, await peer.GetStream().ReadAsync(new byte[1], deadline.Token));
                }
                finally
                {
                    peer.Dispose();
                    server.OnDataReceived -= handler;
                }
            }
        }
        finally
        {
            await server.DisposeAsync().AsTask().WaitAsync(Timeout);
        }
        await Assert.ThrowsAsync<ObjectDisposedException>(() => server.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StopAsync_AlreadyDisconnectedClient_WaitsForCallbackCleanup()
    {
        using var release = new ManualResetEventSlim();
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));
        server.OnDataReceived += (_, args) =>
        {
            _ = args.Client.CloseAsync();
            disconnected.TrySetResult();
            if (!release.Wait(Timeout))
            {
                throw new TimeoutException("The data callback was not released.");
            }
        };
        await server.StartAsync(CancellationToken.None);
        using var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(Timeout);
        try
        {
            await peer.GetStream().WriteAsync(new byte[] { 1 });
            await disconnected.Task.WaitAsync(Timeout);
            var stop = server.StopAsync(CancellationToken.None);
            Assert.False(stop.IsCompleted);
            release.Set();
            await stop.WaitAsync(Timeout);
        }
        finally
        {
            release.Set();
        }
        Assert.Equal(0, server.Port);
    }

    [Fact]
    public async Task StopAsync_BlockedFirstClient_ClosesOtherClientsBeforeDraining()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondConnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connections = 0;
        await using var server = new MoongateTcpServer(new(IPAddress.Loopback, 0));
        server.OnClientConnect += (_, _) =>
        {
            if (Interlocked.Increment(ref connections) == 2)
            {
                secondConnected.TrySetResult();
            }
        };
        server.OnDataReceived += (_, _) =>
        {
            entered.TrySetResult();
            if (!release.Wait(Timeout))
            {
                throw new TimeoutException("The first client callback was not released.");
            }
        };
        await server.StartAsync(CancellationToken.None);
        using var first = new TcpClient();
        using var second = new TcpClient();
        await first.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(Timeout);
        await second.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(Timeout);
        try
        {
            await secondConnected.Task.WaitAsync(Timeout);
            await first.GetStream().WriteAsync(new byte[] { 1 });
            await entered.Task.WaitAsync(Timeout);
            var stop = server.StopAsync(CancellationToken.None);
            Assert.Equal(0, await second.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(Timeout));
            Assert.False(stop.IsCompleted);
            release.Set();
            await stop.WaitAsync(Timeout);
        }
        finally
        {
            release.Set();
        }
    }
}
