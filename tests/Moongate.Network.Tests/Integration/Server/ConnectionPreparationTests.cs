using System.Net;
using System.Net.Sockets;
using Moongate.Network.Data;
using Moongate.Network.Data.Config;
using Moongate.Network.Server;

namespace Moongate.Network.Tests.Integration.Server;

public sealed class ConnectionPreparationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task PrepareAsync_SlowFirstConnection_DoesNotBlockSecondConnection()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connected = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var next = 0;
        await using var server = MoongateTcpServer.CreateConfigured(new IPEndPoint(IPAddress.Loopback, 0), new TcpServerOptions
        {
            ConnectionPipelineFactory = () =>
            {
                var number = Interlocked.Increment(ref next);
                return new ConnectionPipeline
                {
                    PrepareStreamAsync = async (stream, token) =>
                    {
                        if (number == 1)
                        {
                            entered.TrySetResult();
                            await release.Task.WaitAsync(token);
                        }
                        return stream;
                    },
                    ConfigureClient = client => client.OnConnected += (_, _) => connected.TrySetResult(number)
                };
            }
        });
        await server.StartAsync(CancellationToken.None);
        using var first = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var second = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await first.ConnectAsync(server.Endpoint);
            await entered.Task.WaitAsync(Timeout);
            await second.ConnectAsync(server.Endpoint);
            Assert.Equal(2, await connected.Task.WaitAsync(Timeout));
        }
        finally { release.TrySetResult(); }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task Admission_AtConfiguredLimit_RejectsExcessSocket(bool preparing)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await using var server = MoongateTcpServer.CreateConfigured(new IPEndPoint(IPAddress.Loopback, 0), new TcpServerOptions
        {
            MaxConnections = preparing ? 2 : 1,
            MaxConcurrentPreparations = 1,
            ConnectionPipelineFactory = () => new ConnectionPipeline
            {
                PrepareStreamAsync = async (stream, token) =>
                {
                    Interlocked.Increment(ref calls);
                    if (preparing)
                    {
                        entered.TrySetResult();
                        await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token);
                    }
                    return stream;
                },
                ConfigureClient = client => client.OnConnected += (_, _) => entered.TrySetResult()
            }
        });
        await server.StartAsync(CancellationToken.None);
        using var first = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await first.ConnectAsync(server.Endpoint);
        await entered.Task.WaitAsync(Timeout);
        using var second = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await second.ConnectAsync(server.Endpoint);
        Assert.Equal(0, await second.ReceiveAsync(new byte[1], SocketFlags.None).WaitAsync(Timeout));
        Assert.Equal(1, Volatile.Read(ref calls));
        await server.StopAsync(CancellationToken.None).WaitAsync(Timeout);
    }

    [Fact]
    public async Task StopAsync_PreparationInProgress_CancelsAndCanRestart()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connected = 0;
        await using var server = MoongateTcpServer.CreateConfigured(new IPEndPoint(IPAddress.Loopback, 0), new TcpServerOptions
        {
            ConnectionPipelineFactory = () => new ConnectionPipeline
            {
                PrepareStreamAsync = async (stream, token) =>
                {
                    entered.TrySetResult();
                    try { await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token); }
                    finally { cancelled.TrySetResult(); }
                    return stream;
                }
            }
        });
        server.OnClientConnect += (_, _) => Interlocked.Increment(ref connected);
        await server.StartAsync(CancellationToken.None);
        using var peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await peer.ConnectAsync(server.Endpoint);
        await entered.Task.WaitAsync(Timeout);
        await server.StopAsync(CancellationToken.None).WaitAsync(Timeout);
        await cancelled.Task.WaitAsync(Timeout);
        Assert.Equal(0, Volatile.Read(ref connected));
        Assert.Equal(0, await peer.ReceiveAsync(new byte[1], SocketFlags.None).WaitAsync(Timeout));
        await server.StartAsync(CancellationToken.None);
        Assert.True(server.IsRunning);
    }

    [Fact]
    public async Task StopAsync_LatePreparation_DoesNotEnterNextGeneration()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var connectionCount = 0;
        await using var server = MoongateTcpServer.CreateConfigured(new IPEndPoint(IPAddress.Loopback, 0), new TcpServerOptions
        {
            ConnectionPipelineFactory = () => new ConnectionPipeline
            {
                PrepareStreamAsync = async (stream, _) =>
                {
                    if (Interlocked.Increment(ref attempts) == 1)
                    {
                        entered.TrySetResult();
                        await release.Task;
                    }
                    return stream;
                }
            }
        });
        server.OnClientConnect += (_, _) => { Interlocked.Increment(ref connectionCount); connected.TrySetResult(); };
        await server.StartAsync(CancellationToken.None);
        using var first = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await first.ConnectAsync(server.Endpoint);
        await entered.Task.WaitAsync(Timeout);
        var stopping = server.StopAsync(CancellationToken.None);
        release.TrySetResult();
        await stopping.WaitAsync(Timeout);
        Assert.Equal(0, Volatile.Read(ref connectionCount));
        await server.StartAsync(CancellationToken.None);
        using var second = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await second.ConnectAsync(server.Endpoint);
        await connected.Task.WaitAsync(Timeout);
        Assert.Equal(1, Volatile.Read(ref connectionCount));
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task Setup_FailsOrClosesBeforeStart_DoesNotPublishConnected(bool throws)
    {
        var connected = 0;
        await using var server = MoongateTcpServer.CreateConfigured(new IPEndPoint(IPAddress.Loopback, 0), new TcpServerOptions
        {
            ConnectionPipelineFactory = () => new ConnectionPipeline
            {
                PrepareStreamAsync = (stream, _) => throws
                    ? ValueTask.FromException<Stream>(new IOException("setup failed"))
                    : ValueTask.FromResult(stream),
                ConfigureClient = client => client.Dispose()
            }
        });
        server.OnClientConnect += (_, _) => Interlocked.Increment(ref connected);
        await server.StartAsync(CancellationToken.None);
        using var peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await peer.ConnectAsync(server.Endpoint);
        Assert.Equal(0, await peer.ReceiveAsync(new byte[1], SocketFlags.None).WaitAsync(Timeout));
        await server.StopAsync(CancellationToken.None).WaitAsync(Timeout);
        Assert.Equal(0, Volatile.Read(ref connected));
    }
}
