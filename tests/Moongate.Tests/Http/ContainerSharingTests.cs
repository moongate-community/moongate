using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moongate.Http.Plugin.Extensions;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http;

/// <summary>
/// The REST API is only useful if it answers about the world the game loop is actually running.
/// <c>WithDependencyInjectionAdapter</c> returns a <em>new</em> container and applies
/// MicrosoftDependencyInjectionRules, so that sharing is an assumption worth pinning: a cloned registry
/// would hand endpoints a parallel, empty world and still answer 200 OK.
/// </summary>
public class ContainerSharingTests
{
    /// <summary>Stands in for any Moongate singleton the game loop holds.</summary>
    private sealed class GameSingleton
    {
        public string Marker { get; set; } = "from-the-game-loop";
    }

    /// <summary>
    /// An endpoint group shaped like a real one: it takes its dependency through the constructor, which
    /// is where DryIoc hands it the game's instance.
    /// </summary>
    private sealed class SingletonProbeEndpoints : IApiEndpointRegistration
    {
        private readonly GameSingleton _singleton;

        public SingletonProbeEndpoints(GameSingleton singleton)
        {
            _singleton = singleton;
        }

        public void Register(IEndpointRouteBuilder routes)
            => routes.MapGet("/singleton-marker", () => Results.Text(_singleton.Marker));
    }

    [Fact]
    public async Task Endpoint_ServesMutationsMadeThroughTheGameContainerAfterStartup()
    {
        await using var server = await TestHttpServer.StartAsync(
                                     container =>
                                     {
                                         container.Register<GameSingleton>(Reuse.Singleton);
                                         container.RegisterApiEndpoint<SingletonProbeEndpoints>();
                                     }
                                 );

        // Mutated after the routes are mapped, through the container rather than the endpoint: the marker
        // only comes back changed if the endpoint holds the game's own instance and reads it live. This is
        // the whole chain the previous test only covers a link of.
        server.Container.Resolve<GameSingleton>().Marker = "mutated-on-the-loop";

        Assert.Equal("mutated-on-the-loop", await server.Client.GetStringAsync("/singleton-marker"));
    }

    [Fact]
    public async Task WebApplication_ResolvesTheSameSingletonInstanceTheGameContainerHolds()
    {
        // The container as SquidStdBootstrap leaves it: Moongate services already registered.
        var gameContainer = new Container();
        gameContainer.Register<GameSingleton>(Reuse.Singleton);
        var fromGameLoop = gameContainer.Resolve<GameSingleton>();

        var builder = WebApplication.CreateBuilder();
        builder.Host.UseServiceProviderFactory(new DryIocServiceProviderFactory(gameContainer));
        await using var app = builder.Build();

        var fromWeb = app.Services.GetRequiredService<GameSingleton>();

        // Reference identity, not mere resolvability: a cloned registry would hand back a different
        // instance, and no test asserting "200 OK" would ever notice.
        Assert.Same(fromGameLoop, fromWeb);
    }
}
