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

internal static class AuthRouteExtensions
{
    public static IEndpointRouteBuilder MapAuthRoutes(
        this IEndpointRouteBuilder endpoints,
        MoongateHttpRouteContext context
    )
    {
        if (!context.JwtOptions.IsEnabled || context.AccountService is null)
        {
            return endpoints;
        }

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
}
