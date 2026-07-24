using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Api.Stats;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Stats;
using Moongate.Server.Abstractions.Interfaces.Server;

namespace Moongate.Http.Plugin.Endpoints.Stats;

/// <summary>The shard's public numbers: who is online, how big the world is, how much content is loaded.</summary>
public sealed class StatsEndpoints : IApiEndpointRegistration
{
    private readonly IServerStatsService _stats;
    private readonly MoongateConfig _config;

    public StatsEndpoints(IServerStatsService stats, MoongateConfig config)
    {
        _stats = stats;
        _config = config;
    }

    public void Register(IEndpointRouteBuilder routes)
        => routes.MapGet("/api/v1/stats", Get)
            .WithName("GetServerStats")
            .WithTags("stats")
            .Produces<ServerStatsResponse>()
            .AllowAnonymous();

    internal static ServerStatsResponse ToResponse(ServerStatsSnapshot snapshot)
        => new(
            snapshot.GeneratedAt,
            (long)snapshot.Uptime.TotalSeconds,
            new(snapshot.OnlinePlayers, snapshot.Connections),
            new(snapshot.Accounts, snapshot.ActiveAccounts, snapshot.Characters),
            new(snapshot.Npcs, snapshot.WorldItems),
            new(snapshot.ItemTemplates, snapshot.MobileTemplates)
        );

    /// <summary>Reports the shard's public statistics: players online, accounts, world size and loaded content.</summary>
    /// <remarks>
    /// The figures come from a snapshot recomputed on the game loop every <c>StatsRefreshSeconds</c>;
    /// <c>generatedAt</c> says when it was taken, and equals <c>0001-01-01T00:00:00+00:00</c> until the
    /// first one has been computed. The response caches for the same interval.
    /// </remarks>

    // Reads one volatile reference and nothing else: the counting happens on the game loop, so this route
    // never reads world state from an ASP.NET thread.
    private IResult Get(HttpContext context)
    {
        context.Response.Headers.CacheControl = $"public, max-age={_config.StatsRefreshSeconds}";

        return Results.Ok(ToResponse(_stats.Current));
    }
}
