using System.Security.Cryptography;
using System.Text;
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
    private readonly IRegistrationReadinessService _registrationReadiness;
    private readonly IRegistrationRateLimiter _rateLimiter;

    public RegistrationEndpoints(
        IAccountService accounts,
        IServerSettingsService settings,
        IRegistrationReadinessService registrationReadiness,
        IRegistrationRateLimiter rateLimiter
    )
    {
        _accounts = accounts;
        _settings = settings;
        _registrationReadiness = registrationReadiness;
        _rateLimiter = rateLimiter;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/v1/register", RegisterAccount)
            .WithName("RegisterAccount")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithTags("registration")
            .AllowAnonymous();
        routes.MapPost("/api/v1/register/verify", Verify)
            .WithName("VerifyRegistration")
            .WithTags("registration")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status410Gone)
            .AllowAnonymous();
        routes.MapPost("/api/v1/register/resend", ResendVerification)
            .WithName("ResendRegistrationVerification")
            .WithTags("registration")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
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
        var settings = _settings.Get();

        if (!settings.RegistrationEnabled)
        {
            return Results.Problem("Web registration is disabled.", statusCode: StatusCodes.Status403Forbidden);
        }

        if (!_registrationReadiness.Evaluate(settings.Contacts.Website).Ready)
        {
            return Results.Problem(
                "Registration email is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!_rateLimiter.TryAcquire($"register:ip:{clientKey}"))
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
            AccountRegisterResultType.UsernameTaken or AccountRegisterResultType.EmailTaken => Results.Problem(
                "An account already uses those registration details.",
                statusCode: StatusCodes.Status409Conflict
            ),
            AccountRegisterResultType.UsernameEmpty => ValidationProblem("username", "Username is required."),
            AccountRegisterResultType.UsernameInvalid => ValidationProblem("username", "Username is not valid."),
            AccountRegisterResultType.PasswordEmpty => ValidationProblem("password", "Password is required."),
            AccountRegisterResultType.PasswordInvalid => ValidationProblem("password", "Password is not valid."),
            AccountRegisterResultType.EmailEmpty => ValidationProblem("email", "Email is required."),
            AccountRegisterResultType.EmailInvalid => ValidationProblem("email", "Email is not valid."),
            _ => Results.Problem("Unknown registration result.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Verifies an account's email with its token, activating the account.</summary>
    /// <remarks>Answers 410 for an expired token and 400 for an unknown or already-used token.</remarks>
    private IResult Verify(VerifyEmailRequest request)
        => _accounts.VerifyEmail(request.Token) switch
        {
            AccountVerifyResultType.Verified => Results.Ok(),
            AccountVerifyResultType.ExpiredToken => Results.Problem(
                "Verification token expired.",
                statusCode: StatusCodes.Status410Gone
            ),
            _ => Results.Problem(
                "Invalid or already-used verification token.",
                statusCode: StatusCodes.Status400BadRequest
            )
        };

    /// <summary>Resends the verification message for a matching pending public registration.</summary>
    /// <remarks>
    /// Answers 202 whether or not the supplied identity matches a pending account, so callers cannot
    /// discover registered usernames or email addresses.
    /// </remarks>
    private IResult ResendVerification(ResendVerificationRequest request, HttpContext context)
    {
        if (!_registrationReadiness.Evaluate(_settings.Get().Contacts.Website).Ready)
        {
            return Results.Problem(
                "Registration email is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!_rateLimiter.TryAcquire($"resend:ip:{clientKey}"))
        {
            return Results.Problem(
                "Too many verification resend attempts; try again later.",
                statusCode: StatusCodes.Status429TooManyRequests
            );
        }

        if (!_rateLimiter.TryAcquire(BuildResendTargetKey(request)))
        {
            return Results.Problem(
                "Too many verification resend attempts; try again later.",
                statusCode: StatusCodes.Status429TooManyRequests
            );
        }

        return _accounts.ResendVerification(request.Username, request.Email) switch
        {
            AccountResendResultType.Sent or AccountResendResultType.Ignored => Results.Accepted(),
            AccountResendResultType.UsernameInvalid => ValidationProblem(
                "username",
                "Username is not valid."
            ),
            AccountResendResultType.EmailInvalid => ValidationProblem("email", "Email is not valid."),
            _ => Results.Problem(
                "Unknown verification resend result.",
                statusCode: StatusCodes.Status500InternalServerError
            )
        };
    }

    private static string BuildResendTargetKey(ResendVerificationRequest request)
    {
        var username = (request.Username ?? string.Empty).Trim();
        var email = (request.Email ?? string.Empty).Trim().ToUpperInvariant();
        var target = $"{username}\n{email}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(target));

        return $"resend:target:{Convert.ToHexString(digest)}";
    }

    private static IResult ValidationProblem(string field, string message)
        => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
