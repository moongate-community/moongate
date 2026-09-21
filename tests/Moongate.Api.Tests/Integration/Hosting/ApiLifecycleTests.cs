using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Time.Testing;
using Moongate.Api.Client;
using Moongate.Api.Exceptions;
using Moongate.Api.Interfaces.Connections;
using Moongate.Api.Registry;
using Moongate.Api.Server;
using Moongate.Api.Tests.TestSupport.Contracts;
using Moongate.Api.Tests.TestSupport.Handlers;
using Moongate.Api.Tests.TestSupport.Hosting;
using Moongate.Api.Tests.TestSupport.Security;

namespace Moongate.Api.Tests.Integration.Hosting;

public class ApiLifecycleTests
{
    [Fact]
    public async Task CancelledStopWait_DoesNotUndoShutdown()
    {
        var handler = new GatedHandler();
        await using var pair = await ApiPair.StartAsync(handler);
        var request = pair.ClientConnection.RequestAsync<IncrementRequest, IncrementResponse>(new());
        await handler.Entered.Reader.ReadAsync();
        using var cancellation = new CancellationTokenSource();
        var stopping = pair.Server.StopAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopping);
        handler.Release.SetResult();
        await request;
        await pair.Server.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(pair.Server.Endpoint);
    }

    [Fact]
    public async Task ClientHandshakeBudget_RejectsExcessAndDisposalCancelsSetup()
    {
        using var ca = new TestCertificateAuthority();
        using var certificate = ca.Issue();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        await using var client = new ApiClient(
            new(),
            new() { MaxConcurrentHandshakes = 1 },
            ca.Options(certificate, certificate, "peer"),
            TimeProvider.System
        );
        var connecting = client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, "localhost", "peer");
        using var accepted = await listener.AcceptTcpClientAsync();
        await Assert.ThrowsAsync<ApiBusyException>(
            () =>
                client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, "localhost", "peer")
        );
        await client.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting);
    }

    [Fact]
    public async Task FailedBind_ReleasesResourcesAndAllowsRetry()
    {
        using var ca = new TestCertificateAuthority();
        using var certificate = ca.Issue();
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var endpoint = (IPEndPoint)occupied.LocalEndpoint;
        await using var server = new ApiServer(
            endpoint,
            new(),
            new(),
            ca.Options(certificate, certificate, "peer"),
            TimeProvider.System
        );
        await Assert.ThrowsAsync<SocketException>(() => server.StartAsync());
        Assert.Null(server.Endpoint);
        occupied.Stop();
        await server.StartAsync();
        Assert.Equal(endpoint.Port, server.Endpoint!.Port);
    }

    [Fact]
    public async Task ServerCanSendFirstRequestDuringClientStartup()
    {
        using var ca = new TestCertificateAuthority();
        using var serverCert = ca.Issue();
        using var clientCert = ca.Issue();
        var serverRegistry = new ApiRegistry();
        serverRegistry.RegisterContract<IncrementRequest, IncrementResponse>();
        var clientRegistry = new ApiRegistry();
        clientRegistry.RegisterHandler(() => new IncrementHandler());
        await using var server = new ApiServer(
            new(IPAddress.Loopback, 0),
            serverRegistry,
            new(),
            ca.Options(serverCert, clientCert, "admin"),
            TimeProvider.System
        );
        await using var client = new ApiClient(
            clientRegistry,
            new(),
            ca.Options(clientCert, serverCert, "game"),
            TimeProvider.System
        );
        await server.StartAsync();
        var connecting = client.ConnectAsync(server.Endpoint!, "localhost", "game");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (server.Connections.Count != 1)
        {
            await Task.Delay(1, timeout.Token);
        }

        var first = server.Connections[0]
                          .RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 });
        await connecting;
        Assert.Equal(42, (await first).Value);
        var snapshot =
            Assert.IsAssignableFrom<IList<IApiConnection>>(server.Connections);
        Assert.Throws<NotSupportedException>(snapshot.Clear);
    }

    [Fact]
    public async Task StopDuringHandshake_CancelsOwnedSetupAndAllowsRestart()
    {
        await using var pair = await ApiPair.StartAsync(options: new() { MaxConnections = 1 });
        await pair.ClientConnection.CloseAsync();
        await Task.WhenAll(pair.ClientConnection.Completion, pair.ServerConnection.Completion)
                  .WaitAsync(TimeSpan.FromSeconds(5));
        using var rawClient = new TcpClient();
        await rawClient.ConnectAsync(pair.Server.Endpoint!);
        await pair.Server.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(0, await rawClient.GetStream().ReadAsync(new byte[1], timeout.Token));
        await pair.Server.StartAsync();
        Assert.NotNull(pair.Server.Endpoint);
        await using var replacement = await pair.Client.ConnectAsync(pair.Server.Endpoint!, "localhost", "game");
        Assert.Equal(
            42,
            (await replacement.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 })).Value
        );
    }

    [Fact]
    public async Task StopTimeout_ReportsUnfinishedHandlerAndRetainsOwnershipUntilItExits()
    {
        var clock = new FakeTimeProvider();
        var handler = new GatedHandler(false);
        var pair = await ApiPair.StartAsync(handler, new() { ShutdownTimeout = TimeSpan.FromSeconds(1) }, clock);

        try
        {
            var pending = pair.ClientConnection.RequestAsync<IncrementRequest, IncrementResponse>(new());
            await handler.Entered.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            var stopping = pair.Server.StopAsync();
            clock.Advance(TimeSpan.FromSeconds(1));
            var error = await Assert.ThrowsAsync<TimeoutException>(() => stopping.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Contains("still completing", error.Message);
            Assert.False(pair.ServerConnection.Completion.IsCompleted);
            await Assert.ThrowsAsync<IOException>(() => pending);
            await Assert.ThrowsAsync<InvalidOperationException>(() => pair.Server.StartAsync());
            handler.Release.SetResult();
            await pair.ServerConnection.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            handler.Release.TrySetResult();
            await pair.ServerConnection.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            await pair.DisposeAsync();
        }
    }

    [Fact]
    public async Task Stop_DrainsAdmittedHandlerBeforeClosingTransport()
    {
        var handler = new GatedHandler();
        await using var pair = await ApiPair.StartAsync(handler);
        var pending =
            pair.ClientConnection.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 });
        await handler.Entered.Reader.ReadAsync();
        var stopping = pair.Server.StopAsync();
        Assert.False(stopping.IsCompleted);
        handler.Release.SetResult();
        Assert.Equal(42, (await pending).Value);
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        await pair.Server.StopAsync();
        Assert.Null(pair.Server.Endpoint);
        await pair.Server.StartAsync();
        await using var next = await pair.Client.ConnectAsync(pair.Server.Endpoint!, "localhost", "game");
        Assert.Equal(
            42,
            (await next.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 })).Value
        );
        Assert.NotEqual(pair.ClientConnection.ConnectionId, next.ConnectionId);
    }
}
