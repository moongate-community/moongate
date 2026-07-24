using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Api.Registration;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Registration;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Server;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Http.Plugin.Endpoints.Registration;

/// <summary>Public web self-registration with predisposed email verification.</summary>
public sealed class RegistrationEndpoints : IApiEndpointRegistration
{
    private readonly IAccountService _accounts;
    private readonly IServerSettingsService _settings;
    private readonly IRegistrationRateLimiter _rateLimiter;

    public RegistrationEndpoints(
        IAccountService accounts, IServerSettingsService settings, IRegistrationRateLimiter rateLimiter
    )
    {
        _accounts = accounts;
        _settings = settings;
        _rateLimiter = rateLimiter;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/v1/register", RegisterAccount)
            .WithName("RegisterAccount")
            .Produces(StatusCodes.Status202Accepted)
            .WithTags("registration")
            .AllowAnonymous();
        routes.MapPost("/api/v1/register/verify", Verify)
            .WithName("VerifyRegistration")
            .WithTags("registration")
            .Produces(StatusCodes.Status200OK)
            .AllowAnonymous();
    }

    /// <summary>Registers a new account when web registration is open.</summary>
    /// <remarks>
    /// Answers 403 when registration is disabled, 429 when the caller is rate-limited, 409 for a taken
    /// username, 400 for a missing or malformed field, and 202 on success — the account is created
    /// inactive and must verify its email before it can log in.
    /// </remarks>
    private IResult RegisterAccount(RegisterRequest request, HttpContext context)
    {
        if (!_settings.Get().RegistrationEnabled)
        {
            return Results.Problem("Web registration is disabled.", statusCode: StatusCodes.Status403Forbidden);
        }

        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!_rateLimiter.TryAcquire(clientKey))
        {
            return Results.Problem(
                "Too many registration attempts; try again later.",
                statusCode: StatusCodes.Status429TooManyRequests
            );
        }

        var result = _accounts.RegisterPending(request.Username, request.Password, request.Email);

        return result.Result switch
        {
            AccountRegisterResultType.Created => Results.Accepted(),
            AccountRegisterResultType.UsernameTaken => Results.Problem(
                $"An account named '{request.Username}' already exists.",
                statusCode: StatusCodes.Status409Conflict
            ),
            AccountRegisterResultType.UsernameEmpty => Results.Problem(
                "Username is required.",
                statusCode: StatusCodes.Status400BadRequest
            ),
            AccountRegisterResultType.PasswordEmpty => Results.Problem(
                "Password is required.",
                statusCode: StatusCodes.Status400BadRequest
            ),
            AccountRegisterResultType.EmailEmpty => Results.Problem(
                "Email is required.",
                statusCode: StatusCodes.Status400BadRequest
            ),
            AccountRegisterResultType.EmailInvalid => Results.Problem(
                "Email is not valid.",
                statusCode: StatusCodes.Status400BadRequest
            ),
            _ => Results.Problem("Unknown registration result.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Verifies an account's email with its token, activating the account.</summary>
    /// <remarks>Answers 400 for an unknown or already-used token.</remarks>
    private IResult Verify(VerifyEmailRequest request)
        => _accounts.VerifyEmail(request.Token) switch
        {
            AccountVerifyResultType.Verified => Results.Ok(),
            _ => Results.Problem(
                "Invalid or already-used verification token.",
                statusCode: StatusCodes.Status400BadRequest
            )
        };
}
