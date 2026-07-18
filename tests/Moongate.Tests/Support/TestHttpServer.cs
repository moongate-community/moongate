using DryIoc;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Services.Hosting;

namespace Moongate.Tests.Support;

/// <summary>
/// Boots a real <see cref="HttpServerService" /> on an ephemeral port with a usable signing key, and
/// hands back an <see cref="HttpClient" /> pointed at it. Ephemeral ports keep parallel tests from
/// colliding on a fixed one.
/// </summary>
public sealed class TestHttpServer : IAsyncDisposable
{
    public const string SigningKey = "a-test-signing-key-long-enough-for-hs256";

    private readonly HttpServerService _service;

    private TestHttpServer(HttpServerService service, HttpClient client, int port, IContainer container)
    {
        _service = service;
        Client = client;
        Port = port;
        Container = container;
    }

    public HttpClient Client { get; }

    public int Port { get; }

    /// <summary>The game's container, so a test can reach the singletons the endpoints were built from.</summary>
    public IContainer Container { get; }

    public static async Task<TestHttpServer> StartAsync(Action<IContainer>? configure = null, int port = 0)
    {
        var container = new Container();
        var config = new MoongateHttpConfig
        {
            Address = "127.0.0.1",
            Port = port,
            Jwt = new() { SigningKey = SigningKey, LifetimeMinutes = 60, Issuer = "moongate" }
        };

        container.RegisterInstance(config);
        container.RegisterInstance(TimeProvider.System);
        configure?.Invoke(container);

        var service = new HttpServerService(container, config);
        await service.StartAsync();

        var client = new HttpClient { BaseAddress = new($"http://127.0.0.1:{service.BoundPort}") };

        return new(service, client, service.BoundPort, container);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _service.StopAsync();
    }
}
