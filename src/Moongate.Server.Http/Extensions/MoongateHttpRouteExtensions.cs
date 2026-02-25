using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Data.Results;
using Moongate.Server.Http.Internal;
using Moongate.Server.Http.Json;

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
            endpoints.MapGet("/", (CancellationToken cancellationToken) => HandleRoot(context, cancellationToken))
                     .WithName("Root")
                     .WithSummary("Returns service availability.")
                     .Produces<string>(StatusCodes.Status200OK, "text/plain");
        }

        systemGroup.MapGet("/health", (CancellationToken cancellationToken) => HandleHealth(context, cancellationToken))
                   .WithName("Health")
                   .WithSummary("Returns health probe status.")
                   .Produces<string>(StatusCodes.Status200OK, "text/plain");

        systemGroup.MapGet("/metrics", (CancellationToken cancellationToken) => HandleMetrics(context, cancellationToken))
                   .WithName("Metrics")
                   .WithSummary("Returns Prometheus metrics.")
                   .Produces<string>(StatusCodes.Status200OK, "text/plain")
                   .Produces<string>(StatusCodes.Status404NotFound, "text/plain")
                   .Produces<string>(StatusCodes.Status503ServiceUnavailable, "text/plain");

        if (context.JwtOptions.IsEnabled && context.AuthFacade is not null)
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

        if (context.UsersFacade is not null)
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
                     .Produces<IReadOnlyList<MoongateHttpUser>>(StatusCodes.Status200OK);

            usersGroup.MapGet(
                         "/{accountId}",
                         (string accountId, CancellationToken cancellationToken) =>
                             HandleGetUserById(context, accountId, cancellationToken)
                     )
                     .WithName("UsersGetById")
                     .WithSummary("Returns a user by account id.")
                     .Produces<MoongateHttpUser>(StatusCodes.Status200OK)
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
                     .Produces<MoongateHttpUser>(StatusCodes.Status200OK)
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

    private static IResult HandleHealth(MoongateHttpRouteContext context, CancellationToken cancellationToken)
        => ToTextResult(context.SystemFacade.GetHealthAsync(cancellationToken).GetAwaiter().GetResult());

    private static IResult HandleMetrics(MoongateHttpRouteContext context, CancellationToken cancellationToken)
        => ToTextResult(
            context.SystemFacade.GetMetricsAsync(cancellationToken).GetAwaiter().GetResult(),
            StatusCodes.Status200OK,
            "text/plain; version=0.0.4"
        );

    private static IResult HandleLogin(
        MoongateHttpRouteContext context,
        MoongateHttpLoginRequest request,
        CancellationToken cancellationToken
    )
    {
        if (
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password)
        )
        {
            return TypedResults.Text("username and password are required", statusCode: StatusCodes.Status400BadRequest);
        }

        var authResult = context.AuthFacade!.AuthenticateAsync(request.Username, request.Password, cancellationToken)
                                            .GetAwaiter()
                                            .GetResult();
        if (authResult.Status != MoongateHttpOperationStatus.Ok || authResult.Value is null)
        {
            return TypedResults.Unauthorized();
        }

        var user = authResult.Value;
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

    private static IResult HandleGetUsers(
        MoongateHttpRouteContext context,
        CancellationToken cancellationToken
    )
    {
        var result = context.UsersFacade!.GetUsersAsync(cancellationToken).GetAwaiter().GetResult();

        return ToJsonResult(result);
    }

    private static IResult HandleGetUserById(
        MoongateHttpRouteContext context,
        string accountId,
        CancellationToken cancellationToken
    )
    {
        var result = context.UsersFacade!.GetUserByIdAsync(accountId, cancellationToken).GetAwaiter().GetResult();

        return ToJsonResult(result);
    }

    private static IResult HandleCreateUser(
        MoongateHttpRouteContext context,
        MoongateHttpCreateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = context.UsersFacade!.CreateUserAsync(request, cancellationToken).GetAwaiter().GetResult();

        return ToJsonResult(result);
    }

    private static IResult HandleUpdateUser(
        MoongateHttpRouteContext context,
        string accountId,
        MoongateHttpUpdateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = context.UsersFacade!.UpdateUserAsync(accountId, request, cancellationToken).GetAwaiter().GetResult();

        return ToJsonResult(result);
    }

    private static IResult HandleDeleteUser(
        MoongateHttpRouteContext context,
        string accountId,
        CancellationToken cancellationToken
    )
    {
        var result = context.UsersFacade!.DeleteUserAsync(accountId, cancellationToken).GetAwaiter().GetResult();

        return ToJsonResult(result);
    }

    private static IResult HandleRoot(MoongateHttpRouteContext context, CancellationToken cancellationToken)
        => ToTextResult(context.SystemFacade.GetRootAsync(cancellationToken).GetAwaiter().GetResult());

    private static IResult ToJsonResult<T>(MoongateHttpOperationResult<T> result)
    {
        return result.Status switch
        {
            MoongateHttpOperationStatus.Ok when result.Value is not null
                => TypedResults.Ok(result.Value),
            MoongateHttpOperationStatus.Created when result.Value is not null
                => TypedResults.Created(result.Location ?? string.Empty, result.Value),
            MoongateHttpOperationStatus.NoContent
                => TypedResults.NoContent(),
            MoongateHttpOperationStatus.BadRequest
                => TypedResults.BadRequest(result.Error ?? "bad request"),
            MoongateHttpOperationStatus.Unauthorized
                => TypedResults.Unauthorized(),
            MoongateHttpOperationStatus.NotFound
                => TypedResults.NotFound(),
            MoongateHttpOperationStatus.Conflict
                => TypedResults.Conflict(),
            MoongateHttpOperationStatus.ServiceUnavailable
                => TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable),
            _ => TypedResults.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private static IResult ToTextResult(
        MoongateHttpOperationResult<string> result,
        int okStatusCode = StatusCodes.Status200OK,
        string contentType = "text/plain"
    )
    {
        return result.Status switch
        {
            MoongateHttpOperationStatus.Ok => TypedResults.Text(result.Value ?? string.Empty, contentType, Encoding.UTF8, okStatusCode),
            MoongateHttpOperationStatus.NotFound => TypedResults.Text(
                result.Error ?? "not found",
                "text/plain",
                statusCode: StatusCodes.Status404NotFound
            ),
            MoongateHttpOperationStatus.ServiceUnavailable => TypedResults.Text(
                result.Error ?? "service unavailable",
                "text/plain",
                statusCode: StatusCodes.Status503ServiceUnavailable
            ),
            MoongateHttpOperationStatus.BadRequest => TypedResults.Text(
                result.Error ?? "bad request",
                "text/plain",
                statusCode: StatusCodes.Status400BadRequest
            ),
            _ => TypedResults.Text("internal error", "text/plain", statusCode: StatusCodes.Status500InternalServerError)
        };
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
}
