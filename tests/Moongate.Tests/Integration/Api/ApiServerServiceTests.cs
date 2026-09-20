using System.Net;
using System.Net.Sockets;
using Moongate.Api.Exceptions;
using Moongate.Api.Registry;
using Moongate.Api.Types.Protocol;
using Moongate.Core.Directories;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Api;
using Moongate.Tests.TestSupport.Api;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Environment;
using Moongate.Tests.TestSupport.Scripting;
using Serilog;
using Serilog.Events;

namespace Moongate.Tests.Integration.Api;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class ApiServerServiceTests
{
    [Fact]
    public async Task StartAsync_Disabled_WarnsWithoutBindingLoadingCertificatesOrFreezingRegistry()
    {
        using var directory = new TemporaryDirectory();
        var registry = new ApiRegistry();
        var sink = new CapturingLogSink();
        var previous = Log.Logger;
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        Log.Logger = logger;
        try
        {
            await using var service = new ApiServerService(new ApiConfig(),
                new DirectoriesConfig(directory.Path, ["config"]), registry, TimeProvider.System);
            await service.StartAsync();
            Assert.Null(service.Endpoint);
            Assert.False(registry.IsFrozen);
            Assert.Contains(sink.Events, entry => entry.Level == LogEventLevel.Warning &&
                entry.RenderMessage().Contains("api.enabled"));
        }
        finally { Log.Logger = previous; }
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task StartAsync_ConfiguredEndpoint_AcceptsAuthenticatedCallsAndReleasesPort(bool unencrypted)
    {
        using var fixture = new ApiHostFixture();
        if (unencrypted) { fixture.UseUnencryptedCertificate(); }
        await using var service = fixture.CreateService();
        var first = service.StartAsync();
        Assert.Same(first, service.StartAsync());
        await first;
        Assert.Equal(fixture.Config.Port, service.Endpoint!.Port);
        Assert.True(fixture.Registry.IsFrozen);
        await using var client = fixture.CreateClient();
        await using var connection = await client.ConnectAsync(service.Endpoint, "localhost", "server");
        var response = await connection.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 });
        Assert.Equal(42, response.Value);
        var stop = service.StopAsync();
        Assert.Same(stop, service.StopAsync());
        await stop;
        await connection.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(service.Endpoint);
        fixture.AssertPortReleased();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync());
    }

    [Fact]
    public async Task StartAsync_PeerWithoutPermission_RejectsOperation()
    {
        using var fixture = new ApiHostFixture();
        fixture.Config.Peers[0].AllowedOperations = [];
        await using var service = fixture.CreateService();
        await service.StartAsync();
        await using var client = fixture.CreateClient();
        await using var connection = await client.ConnectAsync(service.Endpoint!, "localhost", "server");
        var exception = await Assert.ThrowsAsync<ApiRemoteException>(() =>
            connection.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 }));
        Assert.Equal(ApiErrorCode.Forbidden, exception.Code);
    }

    [Fact]
    public async Task StartAsync_UnlistedPeer_RejectsConnectionOrCall()
    {
        using var fixture = new ApiHostFixture();
        fixture.Config.Peers[0].CertificateSha256 = new string('B', 64);
        await using var service = fixture.CreateService();
        await service.StartAsync();
        await using var client = fixture.CreateClient();
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var connection = await client.ConnectAsync(service.Endpoint!, "localhost", "server");
            await connection.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 });
        });
    }

    [Theory, InlineData("missing_leaf"), InlineData("missing_root"), InlineData("wrong_password"), InlineData("missing_password")]
    public async Task StartAsync_InvalidTls_FailsWithoutBindingAndDoesNotExposePassword(string failure)
    {
        using var fixture = new ApiHostFixture();
        switch (failure)
        {
            case "missing_leaf": fixture.Config.CertificatePath = "missing.pfx"; break;
            case "missing_root": fixture.Config.TrustedRootPaths = ["tls/root.pem", "missing.pem"]; break;
            case "wrong_password": Environment.SetEnvironmentVariable(fixture.Config.CertificatePasswordEnvironmentVariable, "wrong-test-password"); break;
            case "missing_password": Environment.SetEnvironmentVariable(fixture.Config.CertificatePasswordEnvironmentVariable, null); break;
        }
        await using var service = fixture.CreateService();
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => service.StartAsync());
        Assert.DoesNotContain(fixture.Password, exception.ToString());
        Assert.Null(service.Endpoint);
        await service.StopAsync();
        fixture.AssertPortReleased();
    }

    [Fact]
    public async Task StartAsync_OccupiedPort_FailsAndCanBeStopped()
    {
        using var fixture = new ApiHostFixture();
        using var listener = new TcpListener(IPAddress.Loopback, fixture.Config.Port);
        listener.Start();
        await using var service = fixture.CreateService();
        await Assert.ThrowsAsync<SocketException>(() => service.StartAsync());
        await service.StopAsync();
        Assert.Null(service.Endpoint);
        listener.Stop();
        fixture.AssertPortReleased();
    }

    [Fact]
    public async Task StopAsync_DuringStartup_WaitsForStartupAndReleasesPort()
    {
        using var fixture = new ApiHostFixture();
        await using var service = fixture.CreateService();
        var start = service.StartAsync();
        var stop = service.StopAsync();
        await Task.WhenAll(start, stop).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(service.Endpoint);
        fixture.AssertPortReleased();
    }

    [Fact]
    public async Task StopAsync_BeforeStart_PreventsBinding()
    {
        using var fixture = new ApiHostFixture();
        await using var service = fixture.CreateService();
        await service.StopAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync());
        fixture.AssertPortReleased();
    }
}
