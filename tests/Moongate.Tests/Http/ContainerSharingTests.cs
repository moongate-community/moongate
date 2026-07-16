using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Moongate.Tests.Http;

/// <summary>
/// The REST API is only useful if its endpoints resolve the very singletons the game loop holds.
/// <c>WithDependencyInjectionAdapter</c> returns a <em>new</em> container and applies
/// MicrosoftDependencyInjectionRules, so that sharing is an assumption worth pinning: a cloned
/// registry would hand endpoints a parallel, empty world and still answer 200 OK.
/// </summary>
public class ContainerSharingTests
{
    /// <summary>Stands in for any Moongate singleton the game loop holds.</summary>
    private sealed class GameSingleton
    {
        public string Marker { get; set; } = "from-the-game-loop";
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

    [Fact]
    public async Task WebApplication_SeesMutationsMadeThroughTheGameContainer()
    {
        var gameContainer = new Container();
        gameContainer.Register<GameSingleton>(Reuse.Singleton);

        var builder = WebApplication.CreateBuilder();
        builder.Host.UseServiceProviderFactory(new DryIocServiceProviderFactory(gameContainer));
        await using var app = builder.Build();

        gameContainer.Resolve<GameSingleton>().Marker = "mutated-on-the-loop";

        Assert.Equal("mutated-on-the-loop", app.Services.GetRequiredService<GameSingleton>().Marker);
    }
}
