using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Core.Interfaces;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Services;
using Moongate.Persistence.Entities;
using Moongate.Http.Plugin.Data.Api.Accounts;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Types;
using Serilog;

namespace Moongate.Http.Plugin.Endpoints.Accounts;

/// <summary>Account administration: the REST twin of Lua's <c>account.*</c> module.</summary>
public sealed class AccountEndpoints : IApiEndpointRegistration
{
    private readonly ILogger _logger = Log.ForContext<AccountEndpoints>();
    private readonly IAccountService _accounts;
    private readonly IGameLoopContext _loop;

    public AccountEndpoints(IAccountService accounts, IGameLoopContext loop)
    {
        _accounts = accounts;
        _loop = loop;
    }

    /// <summary>
    /// How long to wait for the game loop before calling it unresponsive. Init-only rather than a
    /// mutable static: xUnit runs test classes in parallel, and one test shortening a shared value would
    /// change another's timeout mid-run.
    /// </summary>
    public TimeSpan DeleteTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public void Register(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/admin/accounts")
                          .WithTags("accounts")
                          .RequireAuthorization(HttpServerService.AdminPolicy);

        group.MapGet("/", List).WithName("ListAccounts");
        group.MapGet("/{username}", Get).WithName("GetAccount");
        group.MapPost("/", Create).WithName("CreateAccount");
        group.MapPatch("/{username}", Update).WithName("UpdateAccount");
        group.MapDelete("/{username}", Delete).WithName("DeleteAccount");
    }

    /// <summary>Deletes an account, along with its characters and everything they carry.</summary>
    /// <remarks>
    /// Permanent, and the only account route that changes the world rather than just the account record.
    /// An account whose character is currently being played cannot be deleted: the request answers 409
    /// until that character logs out. A 503 means the game loop did not respond and nothing was deleted.
    /// </remarks>
    private async Task<IResult> Delete(string username)
    {
        // Runs on the game loop rather than here. This checks IsCharacterPlayed and then deletes, and
        // login runs on the loop — so off the loop a player can log in between the two and lose their
        // character from under them. The stores lock internally, so the damage would not be corruption; it
        // would be a silent, permanent loss. Handing the work to the loop puts check and act on login's
        // own thread, where they cannot interleave.
        //
        // The timeout is why a stalled loop fails the request instead of hanging it forever, and 503 is
        // the honest answer: the game loop is not responding.
        var completion = new TaskCompletionSource<AccountDeleteResultType>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        _loop.Post(() => completion.SetResult(_accounts.Delete(username)));

        AccountDeleteResultType result;

        try
        {
            result = await completion.Task.WaitAsync(DeleteTimeout);
        }
        catch (TimeoutException)
        {
            _logger.Error(
                "Account delete for {Username} timed out after {Timeout}; the game loop did not answer",
                username,
                DeleteTimeout
            );

            return Results.Problem(
                "The game loop did not respond; the account was not deleted.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        return result switch
        {
            AccountDeleteResultType.Deleted => Results.NoContent(),
            AccountDeleteResultType.NotFound => NotFound(username),
            AccountDeleteResultType.CharacterBeingPlayed => Results.Problem(
                $"'{username}' has a character being played; it cannot be deleted right now.",
                statusCode: StatusCodes.Status409Conflict
            ),
            _ => Results.Problem(
                "Unknown account delete result.",
                statusCode: StatusCodes.Status500InternalServerError
            )
        };
    }

    /// <summary>Lists every account on the shard.</summary>
    /// <remarks>Passwords are never returned, by any account route.</remarks>
    private IResult List()
        => Results.Ok(_accounts.GetAll().Select(ToResponse));

    /// <summary>Fetches a single account by username.</summary>
    /// <remarks>Answers 404 when no account carries that username.</remarks>
    private IResult Get(string username)
        => _accounts.GetByUsername(username) is { } account
               ? Results.Ok(ToResponse(account))
               : NotFound(username);

    /// <summary>Creates an account.</summary>
    /// <remarks>
    /// Username and password are required, and the username must be free — a taken one answers 409. Level
    /// is optional and defaults to Player; when sent it must name an account level, case-insensitively.
    /// </remarks>
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

    /// <summary>Updates an account, changing only the fields that are sent.</summary>
    /// <remarks>
    /// Every field is optional; an omitted one is left as it is. Answers 404 for an unknown account, and
    /// 400 when the level does not name one. Returns the account as it stands after the update.
    /// </remarks>
    private IResult Update(string username, UpdateAccountRequest request)
    {
        // Applying several fields is not atomic: nothing rolls the level back if a later setter fails.
        // Accepted rather than solved — the setters fail only when the account is missing, which is ruled
        // out a line below, so a delete would have to race this patch for the window to open at all, and
        // closing it properly needs a transaction the store does not offer. The level, the one input a
        // caller can get wrong, is validated before any setter runs.
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
