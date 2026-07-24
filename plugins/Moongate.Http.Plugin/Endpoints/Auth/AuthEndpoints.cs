using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Api.Auth;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Interfaces.Auth;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Auth;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Serilog;

namespace Moongate.Http.Plugin.Endpoints.Auth;

/// <summary>Trades account credentials for a bearer token.</summary>
public sealed class AuthEndpoints : IApiEndpointRegistration
{
    private readonly ILogger _logger = Log.ForContext<AuthEndpoints>();
    private readonly IAccountService _accounts;
    private readonly IJwtTokenService _tokens;
    private readonly MoongateHttpConfig _config;
    private readonly TimeProvider _timeProvider;

    public AuthEndpoints(
        IAccountService accounts,
        IJwtTokenService tokens,
        MoongateHttpConfig config,
        TimeProvider timeProvider
    )
    {
        _accounts = accounts;
        _tokens = tokens;
        _config = config;
        _timeProvider = timeProvider;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/v1/auth/login", Login)
            .WithName("Login")
            .WithTags("auth")
            .Produces<ApiTokenResult>()
            .AllowAnonymous();

        routes.MapPost("/api/v1/auth/renew", Renew)
            .WithName("RenewToken")
            .WithTags("auth")
            .Produces<ApiTokenResult>()
            .RequireAuthorization(HttpServerService.PlayerPolicy);
    }

    /// <summary>Trades account credentials for a bearer token.</summary>
    /// <remarks>
    /// The token carries the account's username and level, and every other route reads authorisation from
    /// it. Send it as <c>Authorization: Bearer &lt;token&gt;</c>. Any failure answers 401 without saying
    /// which part was wrong.
    /// </remarks>
    private IResult Login(LoginRequest request)
    {
        var result = _accounts.Authenticate(request.Username, request.Password);

        if (!result.Success)
        {
            // One flat 401 whatever the reason. AccountAuthResult distinguishes bad credentials from a
            // blocked account, and the game client needs that — but over HTTP it would tell an attacker
            // which usernames exist. The real reason goes to the log instead.
            _logger.Information("API login denied for {Username}: {Reason}", request.Username, result.Reason);

            return Results.Unauthorized();
        }

        if (_accounts.GetByUsername(request.Username) is not { } account)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(
            _tokens.Issue(account.Id, account.Username, account.AccountLevel, _timeProvider.GetUtcNow())
        );
    }

    /// <summary>Exchanges a still-valid token for a fresh one, so an active session does not expire mid-use.</summary>
    /// <remarks>
    /// Send the current token as <c>Authorization: Bearer &lt;token&gt;</c>; the reply has the same shape as
    /// <c>/api/v1/auth/login</c>. The new token keeps the <c>auth_time</c> of the one presented, so renewing
    /// never restarts the session: past <c>http.Jwt.MaxSessionHours</c> from the original login the answer is
    /// 401 and the user must sign in again. A 401 also means the account has been deactivated.
    /// </remarks>

    // The account is re-read rather than trusted from the claims. That is what gives the shard a coarse
    // revocation it otherwise has none of: deactivating an account stops its renewals, and a level change
    // takes effect on the next one. It is not instant — the token already issued stays valid until it
    // expires — but it bounds the window to one token lifetime instead of forever.
    private IResult Renew(ClaimsPrincipal user)
    {
        if (user.FindFirstValue(ClaimTypes.Name) is not { } username)
        {
            return Results.Unauthorized();
        }

        if (!long.TryParse(user.FindFirstValue(JwtTokenService.AuthTimeClaim), out var authTimeSeconds))
        {
            // A token minted before this claim existed. It cannot be capped, so it is not renewed.
            return Results.Unauthorized();
        }

        var authTime = DateTimeOffset.FromUnixTimeSeconds(authTimeSeconds);

        if (_timeProvider.GetUtcNow() - authTime >= TimeSpan.FromHours(_config.Jwt.MaxSessionHours))
        {
            _logger.Information("Token renewal refused for {Username}: session past the cap", username);

            return Results.Unauthorized();
        }

        if (_accounts.GetByUsername(username) is not { IsActive: true } account)
        {
            _logger.Information("Token renewal refused for {Username}: account missing or deactivated", username);

            return Results.Unauthorized();
        }

        return Results.Ok(_tokens.Issue(account.Id, account.Username, account.AccountLevel, authTime));
    }
}
