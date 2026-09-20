using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Moongate.Api.Client;
using Moongate.Api.Data.Config;
using Moongate.Api.Data.Security;
using Moongate.Api.Exceptions;
using Moongate.Api.Registry;
using Moongate.Api.Types.Protocol;
using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Api;
using Moongate.Server.Services.Api.Internal;
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

    [Fact]
    public async Task StartAsync_DisabledWithGeneration_ProvisionsIdentityWithoutOpeningPortOrFreezingRegistry()
    {
        using var directory = new TemporaryDirectory();
        var config = TomlUtils.Deserialize<ApiConfig>("""
            enabled = false
            auto_generate_certificate = true
            certificate_path = "tls/server.pfx"
            certificate_password_environment_variable = ""
            """);
        var directories = new DirectoriesConfig(directory.Path, ["config"]);
        var registry = new ApiRegistry();
        await using var service = new ApiServerService(config, directories, registry, TimeProvider.System);
        await service.StartAsync();
        Assert.True(File.Exists(Path.Combine(directories["config"], "tls/server.pfx")));
        Assert.True(File.Exists(Path.Combine(directories["config"], "tls/server.pfx.pem")));
        Assert.Null(service.Endpoint);
        Assert.False(registry.IsFrozen);
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

    [Theory, InlineData("allowed"), InlineData("hostname"), InlineData("unlisted_peer"), InlineData("untrusted_root"), InlineData("forbidden")]
    public async Task StartAsync_GeneratedIdentities_RequireMutualTrustNamesAndPermissions(string policy)
    {
        using var fixture = new ApiHostFixture();
        var clientConfig = new ApiConfig
        {
            AutoGenerateCertificate = true, CertificatePath = "tls/client.pfx",
            CertificatePasswordEnvironmentVariable = ""
        };
        using var clientCertificate = new ApiCertificateStore().Load(clientConfig, fixture.Directories, TimeProvider.System);
        fixture.Config.AutoGenerateCertificate = true;
        fixture.Config.CertificatePasswordEnvironmentVariable = "";
        File.Delete(Path.Combine(fixture.Directories["config"], fixture.Config.CertificatePath));
        fixture.Config.TrustedRootPaths = policy == "untrusted_root" ? ["tls/root.pem"] : ["tls/client.pfx.pem"];
        fixture.Config.Peers[0].CertificateSha256 = policy == "unlisted_peer" ? new string('A', 64) : clientCertificate.GetCertHashString(HashAlgorithmName.SHA256);
        if (policy == "forbidden") { fixture.Config.Peers[0].AllowedOperations = new([]); }
        await using var service = fixture.CreateService();
        await service.StartAsync();
        using var serverCertificate = X509CertificateLoader.LoadCertificateFromFile(
            Path.Combine(fixture.Directories["config"], fixture.Config.CertificatePath) + ".pem");
        var registry = new ApiRegistry();
        registry.RegisterContract<IncrementRequest, IncrementResponse>();
        await using var client = new ApiClient(registry, new ApiOptions(), new ApiTlsOptions
        {
            Certificate = clientCertificate, TrustedRoots = [serverCertificate],
            PeersByCertificateSha256 = new Dictionary<string, ApiPeerIdentity>
            {
                [serverCertificate.GetCertHashString(HashAlgorithmName.SHA256)] = new("server", [100])
            }
        }, TimeProvider.System);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        async Task CallAsync()
        {
            await using var connection = await client.ConnectAsync(service.Endpoint!,
                policy == "hostname" ? "wrong.internal" : "localhost", "server", timeout.Token);
            var response = await connection.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 }, cancellationToken: timeout.Token);
            Assert.Equal(42, response.Value);
        }
        if (policy == "allowed") { await CallAsync(); }
        else if (policy == "forbidden")
        {
            var exception = await Assert.ThrowsAsync<ApiRemoteException>(CallAsync);
            Assert.Equal(ApiErrorCode.Forbidden, exception.Code);
        }
        else
        {
            var exception = await Assert.ThrowsAnyAsync<Exception>(CallAsync);
            Assert.False(timeout.IsCancellationRequested, exception.ToString());
        }
    }

    [Theory, InlineData("[\"*\"]", true), InlineData("[100]", true), InlineData("[101]", false), InlineData("[]", false), InlineData(null, false)]
    public async Task StartAsync_OperationPolicyFromToml_EnforcesExplicitPermissions(string? operations, bool allowed)
    {
        using var fixture = new ApiHostFixture();
        var entry = fixture.Config.Peers[0];
        var toml = $"peer_id = \"{entry.PeerId}\"\ncertificate_sha256 = \"{entry.CertificateSha256}\"\n";
        if (operations is not null) { toml += $"allowed_operations = {operations}\n"; }
        fixture.Config.Peers[0] = TomlUtils.Deserialize<ApiPeerConfig>(toml)!;
        await using var service = fixture.CreateService();
        await service.StartAsync();
        await using var client = fixture.CreateClient();
        await using var connection = await client.ConnectAsync(service.Endpoint!, "localhost", "server");
        if (allowed)
        {
            var response = await connection.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 });
            Assert.Equal(42, response.Value);
        }
        else
        {
            var exception = await Assert.ThrowsAsync<ApiRemoteException>(() =>
                connection.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 }));
            Assert.Equal(ApiErrorCode.Forbidden, exception.Code);
        }
    }

    [Fact]
    public async Task StartAsync_PeerWithoutPermission_RejectsOperation()
    {
        using var fixture = new ApiHostFixture();
        fixture.Config.Peers[0].AllowedOperations = new([]);
        await using var service = fixture.CreateService();
        await service.StartAsync();
        await using var client = fixture.CreateClient();
        await using var connection = await client.ConnectAsync(service.Endpoint!, "localhost", "server");
        var exception = await Assert.ThrowsAsync<ApiRemoteException>(() =>
            connection.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 }));
        Assert.Equal(ApiErrorCode.Forbidden, exception.Code);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task StartAsync_UnlistedPeer_RejectsConnectionOrCall(bool allowAllOperations)
    {
        using var fixture = new ApiHostFixture();
        if (allowAllOperations) { fixture.Config.Peers[0].AllowedOperations = new([], allowsAll: true); }
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

    [Theory, InlineData("expired"), InlineData("future"), InlineData("client_only"), InlineData("no_private_key")]
    public async Task StartAsync_InvalidServerCertificate_RejectsBeforeBindingOrResolvingHandlers(string invalidity)
    {
        using var fixture = new ApiHostFixture();
        fixture.UseInvalidServerCertificate(invalidity);
        await using var service = fixture.CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync());
        Assert.Null(service.Endpoint);
        Assert.False(fixture.Registry.IsFrozen);
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
