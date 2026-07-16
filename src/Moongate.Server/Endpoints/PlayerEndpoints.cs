using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Services;
using Moongate.Server.Data.Api;

namespace Moongate.Server.Endpoints;

/// <summary>What an authenticated account may ask about itself.</summary>
public sealed class PlayerEndpoints : IApiEndpointRegistration
{
    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/player/me", Me)
              .WithName("GetPlayerMe")
              .WithTags("player")
              .RequireAuthorization(HttpServerService.PlayerPolicy);
    }

    /// <summary>
    /// Reports only what the token already carries. It deliberately does not list the account's
    /// characters: that would read the mobile store, which is single-writer on the game loop, and drag
    /// loop affinity into a probe endpoint.
    /// </summary>
    private static IResult Me(ClaimsPrincipal user)
        => Results.Ok(
            new PlayerMeResponse(
                user.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                user.FindFirstValue(ClaimTypes.Role) ?? string.Empty
            )
        );
}
