using System.Net;
using System.Net.Sockets;
using Grpc.Core;
using Grpc.Net.Client;
using Moongate.Admin.Contracts.V1;
using Moongate.Core.Directories;
using Moongate.Server.Admin.Data.Config;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Admin.Services;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Tests.TestSupport.Admin;
using Moongate.Tests.TestSupport.Persistence;
using ServerMode = Moongate.Server.Core.Types.Hosting.ServerMode;

namespace Moongate.Tests.Integration.Admin;

public sealed class AdminHostLifecycleTests
{
    [Theory, InlineData(""), InlineData("$UNDEFINED_ADMIN_CERT_TEST")]
    public async Task StartAsync_InvalidCertificate_FailsWithRedactedError(string path)
    {
        using var directory = new TemporaryPersistenceDirectory();
        await using var host = new AdminGrpcHostService(new()
        {
            Enabled = true, CertificatePath = path, CertificatePassword = "$UNDEFINED_ADMIN_PASSWORD_TEST"
        }, new(directory.Path, []), ServerMode.Game, _ => throw new InvalidOperationException("Must not resolve"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(host.StartAsync);
        Assert.Contains("PFX", error.Message);
        Assert.DoesNotContain(directory.Path, error.ToString());
        Assert.DoesNotContain("UNDEFINED_ADMIN_PASSWORD", error.ToString());
    }

    [Fact]
    public async Task StartAsync_Disabled_DoesNotBindOrResolveDependencies()
    {
        using var directory = new TemporaryPersistenceDirectory();
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        await using var host = new AdminGrpcHostService(new()
        {
            Port = ((IPEndPoint)occupied.LocalEndpoint).Port, CertificatePath = "$UNDEFINED_ADMIN_CERT_TEST"
        }, new(directory.Path, []), ServerMode.Game, _ => throw new InvalidOperationException("Must not resolve"));
        await host.StartAsync();
        await host.StopAsync();
    }

    [Theory, InlineData("127.0.0.1"), InlineData("0.0.0.0"), InlineData("*")]
    public async Task StartAsync_TlsGameEndpoint_ReadinessRoleMappingAndTrustAreEnforced(string address)
    {
        await using var backend = await AdminRedisFixture.CreateAsync();
        using var certificates = new AdminTestCertificates();
        using var directory = new TemporaryPersistenceDirectory();
        var port = FreePort();
        using var provider = new TestAdminServerInfoProvider();
        await using var host = new AdminGrpcHostService(new()
        {
            Enabled = true, ListenAddress = address, Port = port, CertificatePath = certificates.PfxPath
        }, new(directory.Path, []), ServerMode.Game, services =>
        {
            AdminGrpcApplication.AddServices(services, new(), backend.Store, backend.Throttle, provider, null, null);
            Assert.Same(provider, services.Single(descriptor => descriptor.ServiceType == typeof(IAdminServerInfoProvider)).ImplementationInstance);
        });
        await host.StartAsync();
        using var channel = GrpcChannel.ForAddress($"https://localhost:{port}", new() { HttpHandler = certificates.CreateHandler() });
        var info = new AdminServer.AdminServerClient(channel);
        Assert.Equal(StatusCode.Unavailable, (await Assert.ThrowsAsync<RpcException>(() => info.GetServerInfoAsync(new()).ResponseAsync)).StatusCode);
        host.Activate();
        Assert.Equal(StatusCode.Unauthenticated, (await Assert.ThrowsAsync<RpcException>(() => info.GetServerInfoAsync(new()).ResponseAsync)).StatusCode);
        Assert.Equal(StatusCode.Unimplemented, (await Assert.ThrowsAsync<RpcException>(() => new AdminLogin.AdminLoginClient(channel).LoginAsync(new()).ResponseAsync)).StatusCode);
        using var wrongName = GrpcChannel.ForAddress($"https://127.0.0.1:{port}", new() { HttpHandler = certificates.CreateHandler() });
        var invalidCertificate = await Assert.ThrowsAsync<RpcException>(() => new AdminServer.AdminServerClient(wrongName).GetServerInfoAsync(new()).ResponseAsync);
        Assert.Contains("RemoteCertificateNameMismatch", invalidCertificate.ToString(), StringComparison.Ordinal);
        host.StopAccepting();
        Assert.Equal(StatusCode.Unavailable, (await Assert.ThrowsAsync<RpcException>(() => info.GetServerInfoAsync(new()).ResponseAsync)).StatusCode);
        await host.StopAsync();
        Assert.Equal(0, provider.DisposeCount);
        Assert.True(backend.Redis.Connection.IsConnected);
        await backend.Store.ResetGateAsync(new(1), false);
    }

    [Fact]
    public async Task StartAsync_PortConflict_FailsWithoutDisposingSharedServices()
    {
        await using var backend = await AdminRedisFixture.CreateAsync();
        using var directory = new TemporaryPersistenceDirectory();
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        await using var host = new AdminGrpcHostService(new()
        {
            Enabled = true, Port = ((IPEndPoint)occupied.LocalEndpoint).Port, AllowInsecureLoopback = true
        }, new(directory.Path, []), ServerMode.Game, services => AdminGrpcApplication.AddServices(services,
            new(), backend.Store, backend.Throttle, new TestAdminServerInfoProvider(), null, null));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(host.StartAsync);
        Assert.DoesNotContain(directory.Path, exception.ToString());
        Assert.True(backend.Redis.Connection.IsConnected);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
