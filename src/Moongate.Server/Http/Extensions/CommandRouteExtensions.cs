using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Extensions.Internal;
using Moongate.Server.Http.Internal;
using Moongate.Server.Http.Json;
using Moongate.Server.Types.Commands;

namespace Moongate.Server.Http.Extensions;

internal static class CommandRouteExtensions
{
    public static IEndpointRouteBuilder MapCommandRoutes(
        this IEndpointRouteBuilder endpoints,
        MoongateHttpRouteContext context
    )
    {
        if (context.CommandSystemService is null)
        {
            return endpoints;
        }

        var commandsGroup = endpoints.MapGroup("/api/commands").WithTags("Commands");

        if (context.JwtOptions.IsEnabled)
        {
            commandsGroup.RequireAuthorization();
        }

        commandsGroup.MapPost(
                         "/execute",
                         (
                             MoongateHttpExecuteCommandRequest request,
                             ClaimsPrincipal user,
                             CancellationToken cancellationToken
                         ) => HandleExecuteCommand(context, request, user, cancellationToken)
                     )
                     .WithName("CommandsExecute")
                     .WithSummary("Executes a console command and returns final output lines.")
                     .Accepts<MoongateHttpExecuteCommandRequest>("application/json")
                     .Produces<MoongateHttpExecuteCommandResponse>()
                     .Produces(StatusCodes.Status400BadRequest)
                     .Produces(StatusCodes.Status401Unauthorized)
                     .Produces(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static IResult HandleExecuteCommand(
        MoongateHttpRouteContext context,
        MoongateHttpExecuteCommandRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken
    )
    {
        if (context.JwtOptions.IsEnabled && !HttpRouteAccessHelper.IsAdministrativeUser(user))
        {
            return HttpResponseHelper.ForbidOrUnauthorized(user);
        }

        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return TypedResults.BadRequest("command is required");
        }

        var lines = context.CommandSystemService!
                           .ExecuteCommandWithOutputAsync(
                               request.Command.Trim(),
                               CommandSourceType.Console,
                               null,
                               cancellationToken
                           )
                           .GetAwaiter()
                           .GetResult();

        var response = new MoongateHttpExecuteCommandResponse
        {
            Success = true,
            Command = request.Command.Trim(),
            OutputLines = lines,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        return Results.Json(response, MoongateHttpJsonContext.Default.MoongateHttpExecuteCommandResponse);
    }
}
