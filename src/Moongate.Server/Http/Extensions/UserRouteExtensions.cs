using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Extensions.Internal;
using Moongate.Server.Http.Internal;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Http.Extensions;

internal static class UserRouteExtensions
{
    public static IEndpointRouteBuilder MapUserRoutes(
        this IEndpointRouteBuilder endpoints,
        MoongateHttpRouteContext context
    )
    {
        if (context.AccountService is null || !context.JwtOptions.IsEnabled)
        {
            return endpoints;
        }

        var usersGroup = endpoints.MapGroup("/api/users").WithTags("Users");
        usersGroup.RequireAuthorization();

        usersGroup.MapGet(
                      "/",
                      (ClaimsPrincipal user, CancellationToken cancellationToken) =>
                          HandleGetUsers(context, user, cancellationToken)
                  )
                  .WithName("UsersGetAll")
                  .WithSummary("Returns all users.")
                  .Produces<IReadOnlyList<MoongateHttpUser>>()
                  .Produces(StatusCodes.Status401Unauthorized)
                  .Produces(StatusCodes.Status403Forbidden);

        usersGroup.MapGet(
                      "/{accountId}",
                      (ClaimsPrincipal user, string accountId, CancellationToken cancellationToken) =>
                          HandleGetUserById(context, user, accountId, cancellationToken)
                  )
                  .WithName("UsersGetById")
                  .WithSummary("Returns a user by account id.")
                  .Produces<MoongateHttpUser>()
                  .Produces(StatusCodes.Status401Unauthorized)
                  .Produces(StatusCodes.Status403Forbidden)
                  .Produces(StatusCodes.Status404NotFound);

        usersGroup.MapPost(
                      "/",
                      (
                          ClaimsPrincipal user,
                          MoongateHttpCreateUserRequest request,
                          CancellationToken cancellationToken
                      ) => HandleCreateUser(context, user, request, cancellationToken)
                  )
                  .WithName("UsersCreate")
                  .WithSummary("Creates a new user.")
                  .Accepts<MoongateHttpCreateUserRequest>("application/json")
                  .Produces<MoongateHttpUser>(StatusCodes.Status201Created)
                  .Produces(StatusCodes.Status400BadRequest)
                  .Produces(StatusCodes.Status401Unauthorized)
                  .Produces(StatusCodes.Status403Forbidden)
                  .Produces(StatusCodes.Status409Conflict);

        usersGroup.MapPut(
                      "/{accountId}",
                      (
                          ClaimsPrincipal user,
                          string accountId,
                          MoongateHttpUpdateUserRequest request,
                          CancellationToken cancellationToken
                      ) => HandleUpdateUser(context, user, accountId, request, cancellationToken)
                  )
                  .WithName("UsersUpdate")
                  .WithSummary("Updates a user by account id.")
                  .Accepts<MoongateHttpUpdateUserRequest>("application/json")
                  .Produces<MoongateHttpUser>()
                  .Produces(StatusCodes.Status400BadRequest)
                  .Produces(StatusCodes.Status401Unauthorized)
                  .Produces(StatusCodes.Status403Forbidden)
                  .Produces(StatusCodes.Status404NotFound);

        usersGroup.MapDelete(
                      "/{accountId}",
                      (ClaimsPrincipal user, string accountId, CancellationToken cancellationToken) =>
                          HandleDeleteUser(context, user, accountId, cancellationToken)
                  )
                  .WithName("UsersDelete")
                  .WithSummary("Deletes a user by account id.")
                  .Produces(StatusCodes.Status204NoContent)
                  .Produces(StatusCodes.Status401Unauthorized)
                  .Produces(StatusCodes.Status403Forbidden)
                  .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static IResult HandleCreateUser(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        MoongateHttpCreateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        if (!HttpRouteAccessHelper.IsAdministrativeUser(user))
        {
            return HttpResponseHelper.ForbidOrUnauthorized(user);
        }

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.BadRequest("username and password are required");
        }

        if (!Enum.TryParse<AccountType>(request.Role, true, out var role))
        {
            return TypedResults.BadRequest("invalid role");
        }

        var created = context.AccountService!
                             .CreateAccountAsync(request.Username, request.Password, request.Email, role)
                             .GetAwaiter()
                             .GetResult();

        if (created is null)
        {
            return TypedResults.Conflict();
        }

        var createdUser = MapAccountToHttpUser(created);

        return TypedResults.Created($"/api/users/{createdUser.AccountId}", createdUser);
    }

    private static IResult HandleDeleteUser(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        string accountId,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        if (!HttpRouteAccessHelper.IsAdministrativeUser(user))
        {
            return HttpResponseHelper.ForbidOrUnauthorized(user);
        }

        var parsedId = HttpRouteAccessHelper.ParseAccountIdOrNull(accountId);

        if (!parsedId.HasValue)
        {
            return TypedResults.BadRequest("invalid accountId");
        }

        var deleted = context.AccountService!
                             .DeleteAccountAsync(parsedId.Value)
                             .GetAwaiter()
                             .GetResult();

        return deleted
                   ? TypedResults.NoContent()
                   : TypedResults.NotFound();
    }

    private static IResult HandleGetUserById(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        string accountId,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        if (!HttpRouteAccessHelper.IsAdministrativeUser(user))
        {
            return HttpResponseHelper.ForbidOrUnauthorized(user);
        }

        var parsedId = HttpRouteAccessHelper.ParseAccountIdOrNull(accountId);

        if (!parsedId.HasValue)
        {
            return TypedResults.BadRequest("invalid accountId");
        }

        var account = context.AccountService!
                             .GetAccountAsync(parsedId.Value)
                             .GetAwaiter()
                             .GetResult();

        if (account is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapAccountToHttpUser(account));
    }

    private static IResult HandleGetUsers(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        CancellationToken cancellationToken
    )
    {
        if (!HttpRouteAccessHelper.IsAdministrativeUser(user))
        {
            return HttpResponseHelper.ForbidOrUnauthorized(user);
        }

        var accounts = context.AccountService!
                              .GetAccountsAsync(cancellationToken)
                              .GetAwaiter()
                              .GetResult();
        var users = accounts.Select(MapAccountToHttpUser).ToList();

        return TypedResults.Ok((IReadOnlyList<MoongateHttpUser>)users);
    }

    private static IResult HandleUpdateUser(
        MoongateHttpRouteContext context,
        ClaimsPrincipal user,
        string accountId,
        MoongateHttpUpdateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!HttpRouteAccessHelper.IsAdministrativeUser(user))
        {
            return HttpResponseHelper.ForbidOrUnauthorized(user);
        }

        var parsedId = HttpRouteAccessHelper.ParseAccountIdOrNull(accountId);

        if (!parsedId.HasValue)
        {
            return TypedResults.BadRequest("invalid accountId");
        }

        if (
            request.Username is null &&
            request.Password is null &&
            request.Email is null &&
            request.Role is null &&
            request.IsLocked is null
        )
        {
            return TypedResults.BadRequest("at least one field must be provided");
        }

        AccountType? role = null;

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!Enum.TryParse<AccountType>(request.Role, true, out var parsedRole))
            {
                return TypedResults.BadRequest("invalid role");
            }

            role = parsedRole;
        }

        var updated = context.AccountService!
                             .UpdateAccountAsync(
                                 parsedId.Value,
                                 request.Username,
                                 request.Password,
                                 request.Email,
                                 role,
                                 request.IsLocked,
                                 allowPrivilegeChanges: true,
                                 cancellationToken: cancellationToken
                             )
                             .GetAwaiter()
                             .GetResult();

        return updated is null
                   ? TypedResults.NotFound()
                   : TypedResults.Ok(MapAccountToHttpUser(updated));
    }

    private static MoongateHttpUser MapAccountToHttpUser(UOAccountEntity account)
        => new()
        {
            AccountId = account.Id.Value.ToString(),
            Username = account.Username,
            Email = account.Email,
            Role = account.AccountType.ToString(),
            IsLocked = account.IsLocked,
            CreatedUtc = account.CreatedUtc,
            LastLoginUtc = account.LastLoginUtc,
            CharacterCount = account.CharacterIds.Count
        };
}
