using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Api.Players;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Hosting;

namespace Moongate.Http.Plugin.Endpoints.Players;

/// <summary>What an authenticated account may ask about itself.</summary>
public sealed class PlayerEndpoints : IApiEndpointRegistration
{
    // Produces<T> because the handler returns IResult, which tells the API explorer nothing about the
    // body: without it the route is documented as returning an unspecified 200, and the portal's
    // generated types have no shape to bind to.
    public void Register(IEndpointRouteBuilder routes)
        => routes.MapGet("/api/v1/player/me", Me)
            .WithName("GetPlayerMe")
            .WithTags("player")
            .Produces<PlayerMeResponse>()
            .RequireAuthorization(HttpServerService.PlayerPolicy);

    /// <summary>Reports the account the bearer token belongs to.</summary>
    /// <remarks>
    /// Username and level, read straight from the token — useful for confirming a token is still valid
    /// and what it grants. It does not list the account's characters.
    /// </remarks>

    // Deliberately no character list: that would read the mobile store, which is single-writer on the game
    // loop, and drag loop affinity into a probe endpoint.
    private static IResult Me(ClaimsPrincipal user)
        => Results.Ok(
            new PlayerMeResponse(
                user.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                user.FindFirstValue(ClaimTypes.Role) ?? string.Empty
            )
        );
}
