using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Internal;

namespace Moongate.Server.Http.Extensions;

internal static class SessionRouteExtensions
{
    public static IEndpointRouteBuilder MapSessionRoutes(
        this IEndpointRouteBuilder endpoints,
        MoongateHttpRouteContext context
    )
    {
        if (context.GameNetworkSessionService is null)
        {
            return endpoints;
        }

        var sessionsGroup = endpoints.MapGroup("/api/sessions").WithTags("Sessions");

        if (context.JwtOptions.IsEnabled)
        {
            sessionsGroup.RequireAuthorization();
        }

        sessionsGroup.MapGet(
                         "/active",
                         (CancellationToken cancellationToken) => HandleGetActiveSessions(context, cancellationToken)
                     )
                     .WithName("SessionsGetActive")
                     .WithSummary("Returns currently active in-game sessions.")
                     .Produces<IReadOnlyList<MoongateHttpActiveSession>>();

        return endpoints;
    }

    private static IResult HandleGetActiveSessions(
        MoongateHttpRouteContext context,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        var sessions = context.GameNetworkSessionService!.GetAll()
                              .Where(session => session.AccountId.Value != 0 && session.CharacterId.Value != 0)
                              .OrderBy(session => session.SessionId)
                              .Select(
                                  session =>
                                  {
                                      var account = context.AccountService
                                                           ?
                                                           .GetAccountAsync(session.AccountId)
                                                           .GetAwaiter()
                                                           .GetResult();

                                      return new MoongateHttpActiveSession
                                      {
                                          SessionId = session.SessionId,
                                          AccountId = session.AccountId.Value.ToString(),
                                          Username = account?.Username ?? string.Empty,
                                          AccountType = session.AccountType.ToString(),
                                          CharacterId = session.CharacterId.Value.ToString(),
                                          CharacterName = session.Character?.Name ?? string.Empty,
                                          MapId = session.Character?.MapId ?? 0,
                                          X = session.Character?.Location.X ?? 0,
                                          Y = session.Character?.Location.Y ?? 0
                                      };
                                  }
                              )
                              .ToList();

        return TypedResults.Ok((IReadOnlyList<MoongateHttpActiveSession>)sessions);
    }
}
