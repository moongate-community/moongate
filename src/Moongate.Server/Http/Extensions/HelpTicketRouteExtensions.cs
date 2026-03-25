using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Extensions.Internal;
using Moongate.Server.Http.Internal;
using Moongate.Server.Http.Json;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Http.Extensions;

internal static class HelpTicketRouteExtensions
{
    public static IEndpointRouteBuilder MapHelpTicketRoutes(
        this IEndpointRouteBuilder endpoints,
        MoongateHttpRouteContext context
    )
    {
        if (context.HelpTicketService is null)
        {
            return endpoints;
        }

        var helpTicketsGroup = endpoints.MapGroup("/api/help-tickets").WithTags("HelpTickets");

        if (context.JwtOptions.IsEnabled)
        {
            helpTicketsGroup.RequireAuthorization();
        }

        helpTicketsGroup.MapGet(
                            "/",
                            (
                                ClaimsPrincipal user,
                                int page,
                                int pageSize,
                                string? status,
                                string? category,
                                bool? assignedToMe,
                                CancellationToken cancellationToken
                            ) => HandleGetHelpTickets(
                                context,
                                user,
                                page,
                                pageSize,
                                status,
                                category,
                                assignedToMe,
                                cancellationToken
                            )
                        )
                        .WithName("HelpTicketsGetAll")
                        .WithSummary("Returns all persisted help tickets for staff.")
                        .Produces<MoongateHttpHelpTicketPage>()
                        .Produces(StatusCodes.Status401Unauthorized)
                        .Produces(StatusCodes.Status403Forbidden);

        helpTicketsGroup.MapGet(
                            "/{ticketId}",
                            (ClaimsPrincipal user, string ticketId, CancellationToken cancellationToken) =>
                                HandleGetHelpTicketById(context, user, ticketId, cancellationToken)
                        )
                        .WithName("HelpTicketsGetById")
                        .WithSummary("Returns one persisted help ticket for staff.")
                        .Produces<MoongateHttpHelpTicket>()
                        .Produces(StatusCodes.Status401Unauthorized)
                        .Produces(StatusCodes.Status403Forbidden)
                        .Produces(StatusCodes.Status404NotFound);

        helpTicketsGroup.MapPut(
                            "/{ticketId}/assign-to-me",
                            (ClaimsPrincipal user, string ticketId, CancellationToken cancellationToken) =>
                                HandleAssignHelpTicketToMe(context, user, ticketId, cancellationToken)
                        )
                        .WithName("HelpTicketsAssignToMe")
                        .WithSummary("Assigns a help ticket to the authenticated staff account.")
                        .Produces<MoongateHttpHelpTicket>()
                        .Produces(StatusCodes.Status401Unauthorized)
                        .Produces(StatusCodes.Status403Forbidden)
                        .Produces(StatusCodes.Status404NotFound);

        helpTicketsGroup.MapPut(
                            "/{ticketId}/status",
                            (
                                ClaimsPrincipal user,
                                string ticketId,
                                MoongateHttpUpdateHelpTicketStatusRequest request,
                                CancellationToken cancellationToken
                            ) => HandleUpdateHelpTicketStatus(context, user, ticketId, request, cancellationToken)
                        )
                        .WithName("HelpTicketsUpdateStatus")
                        .WithSummary("Updates a help ticket status for staff.")
                        .Accepts<MoongateHttpUpdateHelpTicketStatusRequest>("application/json")
                        .Produces<MoongateHttpHelpTicket>()
                        .Produces(StatusCodes.Status400BadRequest)
                        .Produces(StatusCodes.Status401Unauthorized)
                        .Produces(StatusCodes.Status403Forbidden)
                        .Produces(StatusCodes.Status404NotFound);

        helpTicketsGroup.MapGet(
                            "/me",
                            (ClaimsPrincipal user, CancellationToken cancellationToken) =>
                                HandleGetMyHelpTickets(context, user, cancellationToken)
                        )
                        .WithName("HelpTicketsGetMine")
                        .WithSummary("Returns the authenticated player's open help tickets.")
                        .Produces<IReadOnlyList<MoongateHttpHelpTicket>>()
                        .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static IResult HandleAssignHelpTicketToMe(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        string ticketId,
        CancellationToken cancellationToken
    )
    {
        if (!HttpRouteAccessHelper.IsAdministrativeUser(user))
        {
            return HttpResponseHelper.ForbidOrUnauthorized(user);
        }

        if (!uint.TryParse(ticketId, out var parsedTicketId))
        {
            return TypedResults.BadRequest("Invalid help ticket id.");
        }

        var accountId = HttpRouteAccessHelper.ResolveAuthenticatedAccountId(user);

        if (accountId is null)
        {
            return TypedResults.Unauthorized();
        }

        var ticket = context.HelpTicketService!
                            .AssignToAccountAsync((Serial)parsedTicketId, accountId.Value, null, cancellationToken)
                            .GetAwaiter()
                            .GetResult();

        if (ticket is null)
        {
            return TypedResults.NotFound();
        }

        return Results.Json(MapHelpTicket(ticket), MoongateHttpJsonContext.Default.MoongateHttpHelpTicket);
    }

    private static IResult HandleGetHelpTicketById(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        string ticketId,
        CancellationToken cancellationToken
    )
    {
        if (!HttpRouteAccessHelper.IsAdministrativeUser(user))
        {
            return HttpResponseHelper.ForbidOrUnauthorized(user);
        }

        if (!uint.TryParse(ticketId, out var parsedTicketId))
        {
            return TypedResults.BadRequest("Invalid help ticket id.");
        }

        var ticket = context.HelpTicketService!
                            .GetTicketByIdAsync((Serial)parsedTicketId, cancellationToken)
                            .GetAwaiter()
                            .GetResult();

        if (ticket is null)
        {
            return TypedResults.NotFound();
        }

        return Results.Json(MapHelpTicket(ticket), MoongateHttpJsonContext.Default.MoongateHttpHelpTicket);
    }

    private static IResult HandleGetHelpTickets(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        int page,
        int pageSize,
        string? status,
        string? category,
        bool? assignedToMe,
        CancellationToken cancellationToken
    )
    {
        if (!HttpRouteAccessHelper.IsAdministrativeUser(user))
        {
            return HttpResponseHelper.ForbidOrUnauthorized(user);
        }

        if (!TryParseHelpTicketStatus(status, out var parsedStatus))
        {
            return TypedResults.BadRequest("Invalid help ticket status.");
        }

        if (!TryParseHelpTicketCategory(category, out var parsedCategory))
        {
            return TypedResults.BadRequest("Invalid help ticket category.");
        }

        var assignedAccountId = assignedToMe == true ? HttpRouteAccessHelper.ResolveAuthenticatedAccountId(user) : null;

        if (assignedToMe == true && assignedAccountId is null)
        {
            return TypedResults.Unauthorized();
        }

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200);
        var (items, totalCount) = context.HelpTicketService!
                                         .GetTicketsForAdminAsync(
                                             safePage,
                                             safePageSize,
                                             parsedStatus,
                                             parsedCategory,
                                             assignedAccountId,
                                             cancellationToken
                                         )
                                         .GetAwaiter()
                                         .GetResult();

        var response = new MoongateHttpHelpTicketPage
        {
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount,
            Items = items.Select(MapHelpTicket).ToList()
        };

        return Results.Json(response, MoongateHttpJsonContext.Default.MoongateHttpHelpTicketPage);
    }

    private static IResult HandleGetMyHelpTickets(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        CancellationToken cancellationToken
    )
    {
        var accountId = HttpRouteAccessHelper.ResolveAuthenticatedAccountId(user);

        if (accountId is null)
        {
            return TypedResults.Unauthorized();
        }

        var tickets = context.HelpTicketService!
                             .GetOpenTicketsForAccountAsync(accountId.Value, cancellationToken)
                             .GetAwaiter()
                             .GetResult()
                             .Select(MapHelpTicket)
                             .ToList();

        return Results.Json(tickets, MoongateHttpJsonContext.Default.IReadOnlyListMoongateHttpHelpTicket);
    }

    private static IResult HandleUpdateHelpTicketStatus(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        string ticketId,
        MoongateHttpUpdateHelpTicketStatusRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!HttpRouteAccessHelper.IsAdministrativeUser(user))
        {
            return HttpResponseHelper.ForbidOrUnauthorized(user);
        }

        if (!uint.TryParse(ticketId, out var parsedTicketId))
        {
            return TypedResults.BadRequest("Invalid help ticket id.");
        }

        if (!Enum.TryParse<HelpTicketStatus>(request.Status?.Trim(), true, out var parsedStatus))
        {
            return TypedResults.BadRequest("Invalid help ticket status.");
        }

        var ticket = context.HelpTicketService!
                            .UpdateStatusAsync((Serial)parsedTicketId, parsedStatus, cancellationToken)
                            .GetAwaiter()
                            .GetResult();

        if (ticket is null)
        {
            return TypedResults.NotFound();
        }

        return Results.Json(MapHelpTicket(ticket), MoongateHttpJsonContext.Default.MoongateHttpHelpTicket);
    }

    private static MoongateHttpHelpTicket MapHelpTicket(HelpTicketEntity ticket)
        => new()
        {
            TicketId = ticket.Id.Value.ToString(),
            SenderCharacterId = ticket.SenderCharacterId.Value.ToString(),
            SenderAccountId = ticket.SenderAccountId.Value.ToString(),
            Category = ticket.Category.ToString(),
            Message = ticket.Message,
            Status = ticket.Status.ToString(),
            MapId = ticket.MapId,
            X = ticket.Location.X,
            Y = ticket.Location.Y,
            Z = ticket.Location.Z,
            CreatedAtUtc = ticket.CreatedAtUtc,
            AssignedAtUtc = ticket.AssignedAtUtc,
            ClosedAtUtc = ticket.ClosedAtUtc,
            LastUpdatedAtUtc = ticket.LastUpdatedAtUtc,
            AssignedToCharacterId = ticket.AssignedToCharacterId.Value.ToString(),
            AssignedToAccountId = ticket.AssignedToAccountId.Value.ToString()
        };

    private static bool TryParseHelpTicketCategory(string? category, out HelpTicketCategory? parsedCategory)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            parsedCategory = null;

            return true;
        }

        if (Enum.TryParse<HelpTicketCategory>(category.Trim(), true, out var value))
        {
            parsedCategory = value;

            return true;
        }

        parsedCategory = null;

        return false;
    }

    private static bool TryParseHelpTicketStatus(string? status, out HelpTicketStatus? parsedStatus)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            parsedStatus = null;

            return true;
        }

        if (Enum.TryParse<HelpTicketStatus>(status.Trim(), true, out var value))
        {
            parsedStatus = value;

            return true;
        }

        parsedStatus = null;

        return false;
    }
}
