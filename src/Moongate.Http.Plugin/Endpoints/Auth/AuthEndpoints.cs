using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Interfaces.Auth;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Data.Api.Auth;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Serilog;

namespace Moongate.Http.Plugin.Endpoints.Auth;

/// <summary>Trades account credentials for a bearer token.</summary>
public sealed class AuthEndpoints : IApiEndpointRegistration
{
    private readonly ILogger _logger = Log.ForContext<AuthEndpoints>();
    private readonly IAccountService _accounts;
    private readonly IJwtTokenService _tokens;

    public AuthEndpoints(IAccountService accounts, IJwtTokenService tokens)
    {
        _accounts = accounts;
        _tokens = tokens;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/v1/auth/login", Login)
              .WithName("Login")
              .WithTags("auth")
              .AllowAnonymous();
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

        return Results.Ok(_tokens.Issue(account.Id, account.Username, account.AccountLevel));
    }
}
