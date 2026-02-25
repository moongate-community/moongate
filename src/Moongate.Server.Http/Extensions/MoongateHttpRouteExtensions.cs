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
        var rootGroup = endpoints.MapGroup(string.Empty).WithTags("Root");
        var systemGroup = endpoints.MapGroup(string.Empty).WithTags("System");

        rootGroup.MapGet("/", HandleRoot)
                 .WithName("Root")
                 .WithSummary("Returns service availability.")
                 .Produces<string>(StatusCodes.Status200OK, "text/plain");

        systemGroup.MapGet("/health", HandleHealth)
                   .WithName("Health")
                   .WithSummary("Returns health probe status.")
                   .Produces<string>(StatusCodes.Status200OK, "text/plain");

        systemGroup.MapGet("/metrics", () => HandleMetrics(context))
                   .WithName("Metrics")
                   .WithSummary("Returns Prometheus metrics.")
                   .Produces<string>(StatusCodes.Status200OK, "text/plain")
                   .Produces<string>(StatusCodes.Status404NotFound, "text/plain")
                   .Produces<string>(StatusCodes.Status503ServiceUnavailable, "text/plain");

        if (context.JwtOptions.IsEnabled)
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

        return endpoints;
    }

    private static IResult HandleHealth()
        => TypedResults.Text("ok");

    private static IResult HandleMetrics(MoongateHttpRouteContext context)
    {
        if (context.MetricsSnapshotFactory is null)
        {
            return TypedResults.Text(
                "metrics endpoint is not configured",
                "text/plain",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        var snapshot = context.MetricsSnapshotFactory();

        if (snapshot is null)
        {
            return TypedResults.Text(
                "metrics are currently unavailable",
                "text/plain",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        var payload = context.PrometheusPayloadBuilder(snapshot);

        return TypedResults.Text(payload, "text/plain; version=0.0.4", Encoding.UTF8, StatusCodes.Status200OK);
    }

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

        var user = context.AuthenticateUserAsync!(request.Username, request.Password, cancellationToken)
                          .GetAwaiter()
                          .GetResult();

        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

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

    private static IResult HandleRoot()
        => TypedResults.Text("Moongate HTTP Service is running.");

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
