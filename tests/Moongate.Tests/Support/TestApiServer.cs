using DryIoc;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Services;
using Moongate.Server.Data.Config;
using Moongate.Server.Endpoints;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Services.Accounts;
using SquidStd.Core.Interfaces.Config;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Support;

/// <summary>
/// Boots the REST API over real game services on fake persistence, with one account: tom / secret. The
/// endpoints under test are the real ones, reached over real HTTP.
/// </summary>
public sealed class TestApiServer : IAsyncDisposable
{
    private readonly HttpServerService _service;

    private TestApiServer(HttpServerService service, HttpClient client, IAccountService accounts)
    {
        _service = service;
        Client = client;
        Accounts = accounts;
    }

    public HttpClient Client { get; }

    public IAccountService Accounts { get; }

    public static async Task<TestApiServer> StartAsync(AccountLevelType level = AccountLevelType.Administrator)
    {
        var container = new Container();
        var persistence = new FakePersistenceService();
        var accounts = new AccountService(
            persistence,
            CharacterServiceFixture.Create(persistence, new EventBusService()),
            new StubSessionManager()
        );
        accounts.Create("tom", "secret", null, level);

        var config = new MoongateHttpConfig
        {
            Address = "127.0.0.1",
            Port = 0,
            Jwt = new() { SigningKey = TestHttpServer.SigningKey, LifetimeMinutes = 60, Issuer = "moongate" }
        };
        var moongateConfig = new MoongateConfig { ShardName = "Moongate", UltimaDirectory = "/tmp" };

        container.RegisterInstance(config);
        container.RegisterInstance(TimeProvider.System);
        container.RegisterInstance(moongateConfig);
        container.RegisterInstance<IAccountService>(accounts);
        container.RegisterInstance<IConfigManagerService>(new StubConfigManagerService());
        container.Register<IJwtTokenService, JwtTokenService>(Reuse.Singleton);

        container.RegisterApiEndpointInstance(new VersionEndpoints(moongateConfig));
        container.RegisterApiEndpointInstance(new AuthEndpoints(accounts, container.Resolve<IJwtTokenService>()));

        var service = new HttpServerService(container, config);
        await service.StartAsync();

        return new(service, new() { BaseAddress = new($"http://127.0.0.1:{service.BoundPort}") }, accounts);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _service.StopAsync();
    }
}
