using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Api.Console;
using Moongate.Http.Plugin.Data.Console;
using Moongate.Http.Plugin.Interfaces.Console;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Types;
using SquidStd.Core.Interfaces.Threading;

namespace Moongate.Http.Plugin.Endpoints.Console;

/// <summary>A REST web-terminal onto the admin command set: POST a command, receive its output over SSE.</summary>
public sealed class ConsoleEndpoints : IApiEndpointRegistration
{
    private readonly IConsoleStreamRegistry _registry;
    private readonly ICommandService _commands;
    private readonly IMainThreadDispatcher _dispatcher;

    public ConsoleEndpoints(IConsoleStreamRegistry registry, ICommandService commands, IMainThreadDispatcher dispatcher)
    {
        _registry = registry;
        _commands = commands;
        _dispatcher = dispatcher;
    }

    public void Register(IEndpointRouteBuilder routes)
        => routes.MapPost("/api/v1/admin/console", Send)
                 .WithName("SendConsoleCommand")
                 .WithTags("console")
                 .RequireAuthorization(HttpServerService.AdminPolicy);

    /// <summary>Runs a console command; its reply lines stream to the given connection's SSE feed.</summary>
    /// <remarks>Returns 202 immediately — the command is dispatched onto the game loop and its output
    /// arrives asynchronously on <c>GET /api/v1/admin/console/stream</c>.</remarks>
    private IResult Send(ConsoleCommandRequest request, ClaimsPrincipal user)
    {
        // Unknown or already-closed connection: nothing to stream to.
        if (!_registry.TryGetWriter(request.ConnectionId, out var writer))
        {
            return TypedResults.NotFound("Unknown connection.");
        }

        // AdminPolicy guarantees an Administrator/GrandMaster role claim; parse it for the actor level.
        if (!Enum.TryParse<AccountLevelType>(user.FindFirstValue(ClaimTypes.Role), out var level))
        {
            return TypedResults.Forbid();
        }

        var invocation = new CommandInvocation(
            CommandSourceType.Rest,
            level,
            null,
            request.Command,
            line => writer.TryWrite(new ConsoleStreamEvent("line", line))
        );

        _dispatcher.Post(
            () =>
            {
                _commands.Execute(invocation);
                writer.TryWrite(new ConsoleStreamEvent("done", request.Command));
            }
        );

        return TypedResults.Accepted((string?)null);
    }
}
