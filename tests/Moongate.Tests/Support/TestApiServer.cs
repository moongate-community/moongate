using System.Net.Http.Json;
using DryIoc;
using Moongate.Core.Interfaces;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Endpoints.Accounts;
using Moongate.Http.Plugin.Endpoints.Admin;
using Moongate.Http.Plugin.Endpoints.Auth;
using Moongate.Http.Plugin.Endpoints.Characters;
using Moongate.Http.Plugin.Endpoints.Players;
using Moongate.Http.Plugin.Endpoints.ServerInfo;
using Moongate.Http.Plugin.Endpoints.Version;
using Moongate.Http.Plugin.Interfaces.Assets;
using Moongate.Http.Plugin.Interfaces.Auth;
using Moongate.Http.Plugin.Services.Assets;
using Moongate.Http.Plugin.Services.Auth;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Server;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.Server;
using SquidStd.Core.Interfaces.Config;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Support;

/// <summary>
/// Boots the REST API over real game services on fake persistence, with one account: tom / secret. The
/// endpoints under test are the real ones, reached over real HTTP.
/// </summary>
public sealed class TestApiServer : IAsyncDisposable
{
    private readonly HttpServerService _service;

    private TestApiServer(
        HttpServerService service,
        HttpClient client,
        IAccountService accounts,
        CharacterService characters,
        StubSessionManager sessions,
        FakePersistenceService persistence,
        ServerSettingsService serverSettings
    )
    {
        _service = service;
        Client = client;
        Accounts = accounts;
        Characters = characters;
        Sessions = sessions;
        Persistence = persistence;
        ServerSettings = serverSettings;
    }

    public HttpClient Client { get; }

    public IAccountService Accounts { get; }

    /// <summary>The real server-settings service behind the endpoints, so a test can seed settings and assets.</summary>
    public ServerSettingsService ServerSettings { get; }

    /// <summary>Real, over the same fake persistence: a test can give an account a character.</summary>
    public CharacterService Characters { get; }

    /// <summary>
    /// The store behind the services, for the things no service creates: a mobile owned by no account is
    /// what every NPC on a real shard looks like here, and there is no other way to seed one.
    /// </summary>
    public FakePersistenceService Persistence { get; }

    /// <summary>Lets a test declare a character as being played, via <see cref="StubSessionManager.Played" />.</summary>
    public StubSessionManager Sessions { get; }

    /// <summary>
    /// Logs in as the account this fixture seeded and keeps the token on the client, so later requests
    /// carry it. Lives here rather than in each test class because the fixture is what knows the
    /// credentials it created.
    /// </summary>
    public async Task AuthenticateAsync()
    {
        var response = await Client.PostAsJsonAsync(
                           "/api/v1/auth/login",
                           new { username = "tom", password = "secret" }
                       );
        var token = await response.Content.ReadFromJsonAsync<ApiTokenResult>();

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token!.Token);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _service.StopAsync();
    }

    public static async Task<TestApiServer> StartAsync(
        AccountLevelType level = AccountLevelType.Administrator,
        IGameLoopContext? loop = null,
        TimeSpan? deleteTimeout = null,
        Action<IContainer>? configure = null
    )
    {
        var container = new Container();
        var persistence = new FakePersistenceService();
        var sessions = new StubSessionManager();
        var bus = new EventBusService();
        var characters = CharacterServiceFixture.Create(persistence, bus, sessions);
        var accounts = new AccountService(persistence, characters, sessions, bus);
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

        // Mirrors production, where the persistence plugin registers it: a service resolved from this
        // container can ask for a store, exactly as it does on a real shard.
        container.RegisterInstance<IPersistenceService>(persistence);
        container.RegisterInstance<IConfigManagerService>(new StubConfigManagerService());
        container.Register<IJwtTokenService, JwtTokenService>(Reuse.Singleton);

        // Inline by default: the delete route's contract is "hand it to the loop and wait", and running
        // the work inline satisfies it with nothing to flake on.
        var gameLoop = loop ?? new StubGameLoopContext();
        container.RegisterInstance(gameLoop);

        container.RegisterApiEndpointInstance(
            new AccountEndpoints(accounts, gameLoop)
            {
                DeleteTimeout = deleteTimeout ?? TimeSpan.FromSeconds(5)
            }
        );
        container.RegisterApiEndpointInstance(new VersionEndpoints(moongateConfig));
        container.RegisterApiEndpointInstance(new AuthEndpoints(accounts, container.Resolve<IJwtTokenService>()));
        container.RegisterApiEndpointInstance(new AdminEndpoints(moongateConfig, sessions));
        container.RegisterApiEndpointInstance(new PlayerEndpoints());
        container.RegisterApiEndpointInstance(new CharacterEndpoints(accounts, characters));

        var serverSettings = new ServerSettingsService(persistence);
        var assetStore = new ServerAssetFileStore(
            Path.Combine(Path.GetTempPath(), "mg-test-assets-" + Guid.NewGuid().ToString("N"))
        );
        container.RegisterInstance<IServerSettingsService>(serverSettings);
        container.RegisterInstance<IServerAssetFileStore>(assetStore);
        container.RegisterApiEndpointInstance(new ServerInfoEndpoints(moongateConfig, serverSettings, assetStore));

        // Lets a test add endpoint groups this fixture cannot know about — the ones the HTTP plugin owns.
        configure?.Invoke(container);

        var service = new HttpServerService(container, config);
        await service.StartAsync();

        return new(
            service,
            new() { BaseAddress = new($"http://127.0.0.1:{service.BoundPort}") },
            accounts,
            characters,
            sessions,
            persistence,
            serverSettings
        );
    }
}
