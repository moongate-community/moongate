using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Api.Players;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Http.Plugin.Endpoints.Players;

/// <summary>Staff-only views over players currently present in the world.</summary>
public sealed class OnlinePlayerAdminEndpoints : IApiEndpointRegistration
{
    private readonly ISessionManager _sessions;

    public OnlinePlayerAdminEndpoints(ISessionManager sessions)
    {
        _sessions = sessions;
    }

    public void Register(IEndpointRouteBuilder routes)
        => routes.MapGet("/api/v1/admin/players/online", List)
            .WithName("ListOnlinePlayers")
            .WithTags("players")
            .Produces<IReadOnlyList<OnlinePlayerMapResponse>>()
            .RequireAuthorization(HttpServerService.AdminPolicy);

    /// <summary>Lists players who have entered the world with map-ready positions.</summary>
    /// <remarks>
    /// Staff only. The response is an unpaged snapshot ordered by character name and serial. Login and
    /// character-selection sessions are excluded. The snapshot is read directly while the game loop may
    /// move or disconnect a player, so one response can be briefly stale or internally inconsistent.
    /// </remarks>
    private IResult List()
    {
        var players = new List<OnlinePlayerMapResponse>();

        foreach (var session in _sessions.All)
        {
            if (session.State != SessionStateType.InWorld || session.Character is not { } character)
            {
                continue;
            }

            players.Add(OnlinePlayerMapResponse.From(session, character));
        }

        return Results.Ok(
            players
                .OrderBy(player => player.CharacterName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(player => player.CharacterSerial, StringComparer.Ordinal)
                .ToArray()
        );
    }
}
