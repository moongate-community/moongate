using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Core.Exceptions.Admin;
using Moongate.Server.Core.Interfaces.Admin;

namespace Moongate.Server.Admin.Authentication;

/// <summary>Authenticates opaque administration tokens against the shared session store.</summary>
public sealed class AdminTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IAdminSessionStore _sessions;

    public AdminTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAdminSessionStore sessions
    ) : base(options, logger, encoder)
    {
        _sessions = sessions;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Anonymous Login and idempotent Logout perform their own bounded checks.
        if (Context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return AuthenticateResult.NoResult();
        }

        if (!AdminToken.TryGetDigest(Request.Headers, out var digest))
        {
            return AuthenticateResult.Fail("Invalid administration credentials.");
        }

        try
        {
            var session = await _sessions.FindAsync(digest, Context.RequestAborted);

            if (session is null) { return AuthenticateResult.Fail("Invalid administration credentials."); }
            var identity = new ClaimsIdentity(
                [
                    new(ClaimTypes.NameIdentifier, session.Identity.AccountId.Value.ToString(CultureInfo.InvariantCulture)),
                    new(ClaimTypes.Role, session.Identity.AccountType.ToString())
                ],
                Scheme.Name
            );

            return AuthenticateResult.Success(new(new(identity), Scheme.Name));
        }
        catch (AdminDependencyUnavailableException)
        {
            Context.Items[AdminAuthorizationPolicies.DependencyFailure] = true;

            return AuthenticateResult.Fail("Administration unavailable.");
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = Context.Items.ContainsKey(AdminAuthorizationPolicies.DependencyFailure)
                                  ? StatusCodes.Status503ServiceUnavailable
                                  : StatusCodes.Status401Unauthorized;

        return Task.CompletedTask;
    }
}
