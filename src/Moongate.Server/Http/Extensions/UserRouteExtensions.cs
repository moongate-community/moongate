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
        if (context.AccountService is null)
        {
            return endpoints;
        }

        var usersGroup = endpoints.MapGroup("/api/users").WithTags("Users");

        if (context.JwtOptions.IsEnabled)
        {
            usersGroup.RequireAuthorization();
        }

        usersGroup.MapGet(
                      "/",
                      (CancellationToken cancellationToken) => HandleGetUsers(context, cancellationToken)
                  )
                  .WithName("UsersGetAll")
                  .WithSummary("Returns all users.")
                  .Produces<IReadOnlyList<MoongateHttpUser>>();

        usersGroup.MapGet(
                      "/{accountId}",
                      (string accountId, CancellationToken cancellationToken) =>
                          HandleGetUserById(context, accountId, cancellationToken)
                  )
                  .WithName("UsersGetById")
                  .WithSummary("Returns a user by account id.")
                  .Produces<MoongateHttpUser>()
                  .Produces(StatusCodes.Status404NotFound);

        usersGroup.MapPost(
                      "/",
                      (MoongateHttpCreateUserRequest request, CancellationToken cancellationToken) =>
                          HandleCreateUser(context, request, cancellationToken)
                  )
                  .WithName("UsersCreate")
                  .WithSummary("Creates a new user.")
                  .Accepts<MoongateHttpCreateUserRequest>("application/json")
                  .Produces<MoongateHttpUser>(StatusCodes.Status201Created)
                  .Produces(StatusCodes.Status400BadRequest)
                  .Produces(StatusCodes.Status409Conflict);

        usersGroup.MapPut(
                      "/{accountId}",
                      (
                          string accountId,
                          MoongateHttpUpdateUserRequest request,
                          CancellationToken cancellationToken
                      ) => HandleUpdateUser(context, accountId, request, cancellationToken)
                  )
                  .WithName("UsersUpdate")
                  .WithSummary("Updates a user by account id.")
                  .Accepts<MoongateHttpUpdateUserRequest>("application/json")
                  .Produces<MoongateHttpUser>()
                  .Produces(StatusCodes.Status400BadRequest)
                  .Produces(StatusCodes.Status404NotFound);

        usersGroup.MapDelete(
                      "/{accountId}",
                      (string accountId, CancellationToken cancellationToken) =>
                          HandleDeleteUser(context, accountId, cancellationToken)
                  )
                  .WithName("UsersDelete")
                  .WithSummary("Deletes a user by account id.")
                  .Produces(StatusCodes.Status204NoContent)
                  .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static IResult HandleCreateUser(
        MoongateHttpRouteContext context,
        MoongateHttpCreateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

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

        var user = MapAccountToHttpUser(created);

        return TypedResults.Created($"/api/users/{user.AccountId}", user);
    }

    private static IResult HandleDeleteUser(
        MoongateHttpRouteContext context,
        string accountId,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;
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
        string accountId,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;
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
        CancellationToken cancellationToken
    )
    {
        var accounts = context.AccountService!
                              .GetAccountsAsync(cancellationToken)
                              .GetAwaiter()
                              .GetResult();
        var users = accounts.Select(MapAccountToHttpUser).ToList();

        return TypedResults.Ok((IReadOnlyList<MoongateHttpUser>)users);
    }

    private static IResult HandleUpdateUser(
        MoongateHttpRouteContext context,
        string accountId,
        MoongateHttpUpdateUserRequest request,
        CancellationToken cancellationToken
    )
    {
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
