using System.Net;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Admin;

internal sealed class AdminGrpcFixture : IAsyncDisposable
{
    private readonly WebApplication _app;
    public AccountAdminFixture Backend { get; }
    public GrpcChannel Channel { get; }
    public AdminRequestGate Gate { get; }

    private AdminGrpcFixture(WebApplication app, AccountAdminFixture backend, AdminRequestGate gate)
    {
        _app = app;
        Backend = backend;
        Gate = gate;
        Channel = GrpcChannel.ForAddress(app.Urls.Single());
    }

    public static async Task<AdminGrpcFixture> CreateAsync(
        ServerMode mode = ServerMode.Login,
        int concurrency = 64,
        Func<IAccountService, IAccountService>? decorateAccounts = null
    )
    {
        var backend = await AccountAdminFixture.CreateAsync();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(
                IPAddress.Loopback,
                0,
                listen => listen.Protocols = HttpProtocols.Http2
            )
        );
        var gate = new AdminRequestGate(concurrency);
        AdminGrpcApplication.AddServices(
            builder.Services,
            new(),
            backend.Redis.Store,
            backend.Redis.Throttle,
            new TestAdminServerInfoProvider(),
            mode == ServerMode.Game ? null : decorateAccounts?.Invoke(backend.Accounts.Service) ?? backend.Accounts.Service,
            mode == ServerMode.Game ? null : backend.Authority
        );
        var app = builder.Build();
        AdminGrpcApplication.Configure(app, mode, gate);
        await app.StartAsync();
        gate.Activate();

        return new(app, backend, gate);
    }

    public async ValueTask DisposeAsync()
    {
        Gate.StopAccepting();
        await _app.StopAsync();
        await _app.DisposeAsync();
        Channel.Dispose();
        await Backend.DisposeAsync();
    }
}
