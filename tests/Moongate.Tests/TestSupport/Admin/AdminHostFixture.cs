using System.Net;
using System.Net.Sockets;
using DryIoc;
using Grpc.Net.Client;
using Moongate.Core.Directories;
using Moongate.Server.Admin;
using Moongate.Server.Admin.Data.Config;
using Moongate.Server.Admin.Services;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.TestSupport.Admin;

internal sealed class AdminHostFixture : IAsyncDisposable
{
    private readonly TemporaryPersistenceDirectory _directory = new();
    private readonly Container _login = new();
    private readonly Container _game = new();
    public AccountAdminFixture Backend { get; }
    public AdminTestCertificates Certificates { get; } = new();
    public GrpcChannel LoginChannel { get; private set; } = null!;
    public GrpcChannel GameChannel { get; private set; } = null!;
    public string LoginEndpoint { get; private set; } = "";
    public string GameEndpoint { get; private set; } = "";
    public IAdminApiService LoginHost => _login.Resolve<IAdminApiService>();
    public IAdminApiService GameHost => _game.Resolve<IAdminApiService>();

    private AdminHostFixture(AccountAdminFixture backend) { Backend = backend; }

    public static async Task<AdminHostFixture> CreateAsync()
    {
        var fixture = new AdminHostFixture(await AccountAdminFixture.CreateAsync());
        try
        {
            fixture.LoginEndpoint = await fixture.StartAsync(fixture._login, ServerMode.Login);
            fixture.GameEndpoint = await fixture.StartAsync(fixture._game, ServerMode.Game);
            Assert.False(fixture._game.IsRegistered<IAccountService>());
            Assert.False(fixture._game.IsRegistered<IAccountAdminAccessService>());
            fixture.LoginChannel = GrpcChannel.ForAddress(fixture.LoginEndpoint, new() { HttpHandler = fixture.Certificates.CreateHandler() });
            fixture.GameChannel = GrpcChannel.ForAddress(fixture.GameEndpoint, new() { HttpHandler = fixture.Certificates.CreateHandler() });
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    private async Task<string> StartAsync(Container container, ServerMode mode)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        container.RegisterInstance(new DirectoriesConfig(_directory.Path, []));
        container.RegisterInstance(new AdminApiConfig { Enabled = true, Port = port, CertificatePath = Certificates.PfxPath });
        container.RegisterInstance(mode);
        container.RegisterInstance<IAdminSessionStore>(Backend.Redis.Store);
        container.RegisterInstance<IAdminLoginThrottle>(Backend.Redis.Throttle);
        container.RegisterInstance<IAdminServerInfoProvider>(new TestAdminServerInfoProvider());
        if (mode == ServerMode.Login)
        {
            container.RegisterInstance<IAccountService>(Backend.Accounts.Service);
            container.RegisterInstance<IAccountAdminAccessService>(Backend.Authority);
        }
        new MoongateAdminPlugin().Register(container);
        var host = container.Resolve<IAdminApiService>();
        await host.StartAsync();
        host.Activate();
        return $"https://localhost:{port}";
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var container in new[] { _game, _login })
        {
            if (container.IsRegistered<IAdminApiService>()) { await container.Resolve<IAdminApiService>().StopAsync(); }
            container.Dispose();
        }
        LoginChannel?.Dispose();
        GameChannel?.Dispose();
        Certificates.Dispose();
        _directory.Dispose();
        await Backend.DisposeAsync();
    }
}
