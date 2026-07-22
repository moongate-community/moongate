using System.Net.Http.Json;
using DryIoc;
using Moongate.Core.Interfaces;
using Moongate.Core.Types;
using Moongate.Http.Plugin;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Endpoints.Accounts;
using Moongate.Http.Plugin.Endpoints.Admin;
using Moongate.Http.Plugin.Endpoints.Auth;
using Moongate.Http.Plugin.Endpoints.Characters;
using Moongate.Http.Plugin.Endpoints.Players;
using Moongate.Http.Plugin.Endpoints.Plugins;
using Moongate.Http.Plugin.Endpoints.Registration;
using Moongate.Http.Plugin.Endpoints.ServerInfo;
using Moongate.Http.Plugin.Endpoints.Stats;
using Moongate.Http.Plugin.Endpoints.Version;
using Moongate.Http.Plugin.Interfaces.Assets;
using Moongate.Http.Plugin.Interfaces.Auth;
using Moongate.Http.Plugin.Services.Assets;
using Moongate.Http.Plugin.Services.Auth;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Http.Plugin.Services.Plugins;
using Moongate.Http.Plugin.Services.Registration;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Interfaces.Plugins;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Server;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.Plugins;
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
        ServerSettingsService serverSettings,
        StubServerStatsService stats
    )
    {
        _service = service;
        Client = client;
        Accounts = accounts;
        Characters = characters;
        Sessions = sessions;
        Persistence = persistence;
        ServerSettings = serverSettings;
        Stats = stats;
    }

    public HttpClient Client { get; }

    public IAccountService Accounts { get; }

    /// <summary>The real server-settings service behind the endpoints, so a test can seed settings and assets.</summary>
    public ServerSettingsService ServerSettings { get; }

    /// <summary>The snapshot the stats route serves, so a test can state the figures it expects to read.</summary>
    public StubServerStatsService Stats { get; }

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
        Action<IContainer>? configure = null,
        TimeProvider? clock = null,
        string? uiDistPath = null
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
            Jwt = new() { SigningKey = TestHttpServer.SigningKey, LifetimeMinutes = 60, Issuer = "moongate" },

            // Through configuration rather than a property, because that is the path production takes. The
            // default is a directory that exists nowhere: the resolver also probes ./ui/dist and the one
            // beside the executable, so a test wanting no portal would otherwise find a real build and
            // start passing or failing depending on whether someone had run npm.
            UiDistPath = uiDistPath ?? Path.Combine(Path.GetTempPath(), "mg-no-ui-" + Guid.NewGuid().ToString("N"))
        };
        var moongateConfig = new MoongateConfig { ShardName = "Moongate", UltimaDirectory = "/tmp" };

        container.RegisterInstance(config);

        // A test that needs to move time forward passes its own; everything else gets the real clock.
        var timeProvider = clock ?? TimeProvider.System;

        container.RegisterInstance(timeProvider);
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
        container.RegisterApiEndpointInstance(
            new AuthEndpoints(accounts, container.Resolve<IJwtTokenService>(), config, timeProvider)
        );
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
        container.RegisterApiEndpointInstance(new ServerSettingsAdminEndpoints(serverSettings, assetStore, config));

        // A low limit (2/window) so a test can prove the throttle without flooding: the 3rd call is denied.
        var rateLimiter = new RegistrationRateLimiter(TimeProvider.System, permitPerWindow: 2, window: TimeSpan.FromMinutes(10));
        container.RegisterApiEndpointInstance(new RegistrationEndpoints(accounts, serverSettings, rateLimiter));

        var stats = new StubServerStatsService();
        container.RegisterInstance<IServerStatsService>(stats);
        container.RegisterApiEndpointInstance(new StatsEndpoints(stats, moongateConfig));

        // The fixture's endpoints all live in Moongate.Http.Plugin, so recording that plugin is what makes
        // their routes attributable — the same join the real bootstrap makes.
        var pluginCatalog = new PluginCatalog();
        pluginCatalog.Record(new MoongateHttpPlugin(), isExternal: false);

        container.RegisterInstance<IPluginCatalog>(pluginCatalog);
        container.RegisterApiEndpointInstance(
            new PluginAdminEndpoints(pluginCatalog, new EndpointPluginRouteInspector())
        );

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
            serverSettings,
            stats
        );
    }
}
