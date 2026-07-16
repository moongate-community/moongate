using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Services;
using Moongate.Persistence.Entities;
using Moongate.Server.Data.Api;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Types;

namespace Moongate.Server.Endpoints;

/// <summary>Account administration: the REST twin of Lua's <c>account.*</c> module.</summary>
public sealed class AccountEndpoints : IApiEndpointRegistration
{
    private readonly IAccountService _accounts;

    public AccountEndpoints(IAccountService accounts)
    {
        _accounts = accounts;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/admin/accounts")
                          .WithTags("accounts")
                          .RequireAuthorization(HttpServerService.AdminPolicy);

        group.MapGet("/", List).WithName("ListAccounts");
        group.MapGet("/{username}", Get).WithName("GetAccount");
        group.MapPost("/", Create).WithName("CreateAccount");
        group.MapPatch("/{username}", Update).WithName("UpdateAccount");
    }

    private IResult List()
        => Results.Ok(_accounts.GetAll().Select(ToResponse));

    private IResult Get(string username)
        => _accounts.GetByUsername(username) is { } account
               ? Results.Ok(ToResponse(account))
               : NotFound(username);

    private IResult Create(CreateAccountRequest request)
    {
        // Before the write, so an unknown level cannot leave a half-made account behind.
        if (!TryParseLevel(request.Level, AccountLevelType.Player, out var level))
        {
            return InvalidLevel(request.Level);
        }

        return _accounts.Create(request.Username, request.Password, request.Email, level) switch
        {
            AccountCreateResultType.Created => Results.Created(
                $"/api/v1/admin/accounts/{request.Username}",
                ToResponse(_accounts.GetByUsername(request.Username)!)
            ),
            AccountCreateResultType.UsernameTaken => Results.Problem(
                $"An account named '{request.Username}' already exists.",
                statusCode: StatusCodes.Status409Conflict
            ),
            AccountCreateResultType.UsernameEmpty => Results.Problem(
                "Username is required.",
                statusCode: StatusCodes.Status400BadRequest
            ),
            AccountCreateResultType.PasswordEmpty => Results.Problem(
                "Password is required.",
                statusCode: StatusCodes.Status400BadRequest
            ),
            _ => Results.Problem(
                "Unknown account creation result.",
                statusCode: StatusCodes.Status500InternalServerError
            )
        };
    }

    /// <summary>
    /// Applies the fields that are present and leaves the rest alone.
    /// <para>
    /// Applying several is not atomic: nothing rolls the level back if a later setter fails. Accepted
    /// rather than solved — the setters fail only when the account is missing, which is ruled out a line
    /// earlier, so a delete would have to race this patch for the window to open at all, and closing it
    /// properly needs a transaction the store does not offer. The level, the one input a caller can get
    /// wrong, is validated before any setter runs.
    /// </para>
    /// </summary>
    private IResult Update(string username, UpdateAccountRequest request)
    {
        if (_accounts.GetByUsername(username) is null)
        {
            return NotFound(username);
        }

        if (!TryParseLevel(request.Level, AccountLevelType.Player, out var level))
        {
            return InvalidLevel(request.Level);
        }

        if (request.Level is not null)
        {
            _accounts.SetLevel(username, level);
        }

        if (request.IsActive is { } isActive)
        {
            _accounts.SetActive(username, isActive);
        }

        if (!string.IsNullOrEmpty(request.Password))
        {
            _accounts.SetPassword(username, request.Password);
        }

        return Results.Ok(ToResponse(_accounts.GetByUsername(username)!));
    }

    /// <summary>
    /// Parses a level name, falling back when none was sent. Callers check this before writing anything,
    /// so a bad level fails the whole request rather than half of it.
    /// </summary>
    internal static bool TryParseLevel(string? name, AccountLevelType fallback, out AccountLevelType level)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            level = fallback;

            return true;
        }

        return Enum.TryParse(name, ignoreCase: true, out level);
    }

    internal static IResult InvalidLevel(string? name)
        => Results.Problem(
            $"'{name}' is not an account level. Valid levels: {string.Join(", ", Enum.GetNames<AccountLevelType>())}.",
            statusCode: StatusCodes.Status400BadRequest
        );

    /// <summary>
    /// CharacterCount comes from the entity's own id list, which is free. It must not come from
    /// <c>ICharacterService.GetPlayerCharacters</c>, which scans the account store and the mobile store
    /// on every call — quadratic over a list, and pointless when the entity already holds the ids.
    /// </summary>
    internal static AccountResponse ToResponse(AccountEntity account)
        => new(
            account.Username,
            account.Email,
            account.AccountLevel.ToString(),
            account.IsActive,
            account.MobileIds.Count
        );

    /// <summary>
    /// Says which account was not found. Unlike login's flat 401, these routes name the reason: the
    /// caller already holds a staff token, so it is operational information for someone entitled to it
    /// rather than an oracle telling an attacker which usernames exist.
    /// </summary>
    internal static IResult NotFound(string username)
        => Results.Problem($"No account named '{username}'.", statusCode: StatusCodes.Status404NotFound);
}
