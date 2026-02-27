using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Internal;
using Moongate.Server.Http.Json;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Http.Extensions;

/// <summary>
/// Route mapping extensions for Moongate HTTP endpoints.
/// </summary>
internal static class MoongateHttpRouteExtensions
{
    public static IEndpointRouteBuilder MapMoongateHttpRoutes(
        this IEndpointRouteBuilder endpoints,
        MoongateHttpRouteContext context
    )
    {
        var systemGroup = endpoints.MapGroup(string.Empty).WithTags("System");

        if (!context.IsUiEnabled)
        {
            endpoints.MapGet("/", HandleRoot)
                     .WithName("Root")
                     .WithSummary("Returns service availability.")
                     .Produces<string>(StatusCodes.Status200OK, "text/plain");
        }

        systemGroup.MapGet("/health", HandleHealth)
                   .WithName("Health")
                   .WithSummary("Returns health probe status.")
                   .Produces<string>(StatusCodes.Status200OK, "text/plain");

        systemGroup.MapGet(
                       "/metrics",
                       (CancellationToken cancellationToken) => HandleMetrics(context, cancellationToken)
                   )
                   .WithName("Metrics")
                   .WithSummary("Returns Prometheus metrics.")
                   .Produces<string>(StatusCodes.Status200OK, "text/plain")
                   .Produces<string>(StatusCodes.Status404NotFound, "text/plain")
                   .Produces<string>(StatusCodes.Status503ServiceUnavailable, "text/plain");

        if (context.JwtOptions.IsEnabled && context.AccountService is not null)
        {
            var authGroup = endpoints.MapGroup("/auth").WithTags("Auth");
            authGroup.MapPost(
                         "/login",
                         (MoongateHttpLoginRequest request, CancellationToken cancellationToken) =>
                             HandleLogin(context, request, cancellationToken)
                     )
                     .WithName("AuthLogin")
                     .WithSummary("Authenticates a user and returns a JWT token.")
                     .Accepts<MoongateHttpLoginRequest>("application/json")
                     .WithMetadata(
                         new ProducesResponseTypeMetadata(
                             StatusCodes.Status200OK,
                             typeof(MoongateHttpLoginResponse),
                             ["application/json"]
                         )
                     )
                     .WithMetadata(
                         new ProducesResponseTypeMetadata(StatusCodes.Status400BadRequest, typeof(string), ["text/plain"])
                     )
                     .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status401Unauthorized));
        }

        if (context.AccountService is not null)
        {
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
        }

        return endpoints;
    }

    private static string CreateJwtToken(
        MoongateHttpAuthenticatedUser user,
        DateTimeOffset expiresAtUtc,
        MoongateHttpJwtOptions options
    )
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Username),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new("account_id", user.AccountId)
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            DateTime.UtcNow,
            expiresAtUtc.UtcDateTime,
            signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
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
        var parsedId = ParseAccountIdOrNull(accountId);

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
        var parsedId = ParseAccountIdOrNull(accountId);

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

    private static IResult HandleHealth(CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        return TypedResults.Text("ok");
    }

    private static IResult HandleLogin(
        MoongateHttpRouteContext context,
        MoongateHttpLoginRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.Text("username and password are required", statusCode: StatusCodes.Status400BadRequest);
        }

        var account = context.AccountService!
                             .LoginAsync(request.Username, request.Password)
                             .GetAwaiter()
                             .GetResult();

        if (account is null)
        {
            return TypedResults.Unauthorized();
        }

        var user = new MoongateHttpAuthenticatedUser
        {
            AccountId = account.Id.Value.ToString(),
            Username = account.Username,
            Role = account.AccountType.ToString()
        };

        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(context.JwtOptions.ExpirationMinutes);
        var token = CreateJwtToken(user, expiresAtUtc, context.JwtOptions);

        var response = new MoongateHttpLoginResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresAtUtc = expiresAtUtc,
            AccountId = user.AccountId,
            Username = user.Username,
            Role = user.Role
        };

        return Results.Json(response, MoongateHttpJsonContext.Default.MoongateHttpLoginResponse);
    }

    private static IResult HandleMetrics(MoongateHttpRouteContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (context.MetricsHttpSnapshotFactory is null)
        {
            return TypedResults.Text("metrics endpoint is not configured", statusCode: StatusCodes.Status404NotFound);
        }

        var snapshot = context.MetricsHttpSnapshotFactory.CreateSnapshot();

        if (snapshot is null)
        {
            return TypedResults.Text(
                "metrics are currently unavailable",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        var payload = MoongateHttpService.BuildPrometheusPayload(snapshot);

        return TypedResults.Text(payload, "text/plain; version=0.0.4", Encoding.UTF8, StatusCodes.Status200OK);
    }

    private static IResult HandleRoot()
        => TypedResults.Text("Moongate HTTP Service is running.");

    private static IResult HandleUpdateUser(
        MoongateHttpRouteContext context,
        string accountId,
        MoongateHttpUpdateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var parsedId = ParseAccountIdOrNull(accountId);

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
                                 cancellationToken
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

    private static Serial? ParseAccountIdOrNull(string accountId)
        => uint.TryParse(accountId, out var parsedId) ? (Serial)parsedId : null;
}
