using Moongate.Server.Http.Data;
using Moongate.Server.Http.Data.Results;
using Moongate.Server.Http.Interfaces.Facades;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Http.Facades;

/// <summary>
/// Users facade backed by <see cref="IAccountService" />.
/// </summary>
public sealed class MoongateHttpUsersFacade : IHttpUsersFacade
{
    private readonly IAccountService _accountService;

    public MoongateHttpUsersFacade(IAccountService accountService)
        => _accountService = accountService;

    public async Task<MoongateHttpOperationResult<MoongateHttpUser>> CreateUserAsync(
        MoongateHttpCreateUserRequest request,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return MoongateHttpOperationResult<MoongateHttpUser>.BadRequest("username and password are required");
        }

        if (!Enum.TryParse<AccountType>(request.Role, true, out var role))
        {
            return MoongateHttpOperationResult<MoongateHttpUser>.BadRequest("invalid role");
        }

        var created = await _accountService.CreateAccountAsync(request.Username, request.Password, request.Email, role);
        if (created is null)
        {
            return MoongateHttpOperationResult<MoongateHttpUser>.Conflict();
        }

        var user = MapAccountToHttpUser(created);

        return MoongateHttpOperationResult<MoongateHttpUser>.Created(user, $"/api/users/{user.AccountId}");
    }

    public async Task<MoongateHttpOperationResult<object?>> DeleteUserAsync(
        string accountId,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        var parsed = ParseAccountIdOrNull(accountId);
        if (!parsed.HasValue)
        {
            return MoongateHttpOperationResult<object?>.BadRequest("invalid accountId");
        }

        var deleted = await _accountService.DeleteAccountAsync(parsed.Value);

        return deleted
            ? MoongateHttpOperationResult<object?>.NoContent()
            : MoongateHttpOperationResult<object?>.NotFound();
    }

    public async Task<MoongateHttpOperationResult<IReadOnlyList<MoongateHttpUser>>> GetUsersAsync(
        CancellationToken cancellationToken = default
    )
    {
        var accounts = await _accountService.GetAccountsAsync(cancellationToken);
        var users = accounts.Select(MapAccountToHttpUser).ToList();

        return MoongateHttpOperationResult<IReadOnlyList<MoongateHttpUser>>.Ok(users);
    }

    public async Task<MoongateHttpOperationResult<MoongateHttpUser>> GetUserByIdAsync(
        string accountId,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        var parsed = ParseAccountIdOrNull(accountId);
        if (!parsed.HasValue)
        {
            return MoongateHttpOperationResult<MoongateHttpUser>.BadRequest("invalid accountId");
        }

        var account = await _accountService.GetAccountAsync(parsed.Value);
        if (account is null)
        {
            return MoongateHttpOperationResult<MoongateHttpUser>.NotFound();
        }

        return MoongateHttpOperationResult<MoongateHttpUser>.Ok(MapAccountToHttpUser(account));
    }

    public async Task<MoongateHttpOperationResult<MoongateHttpUser>> UpdateUserAsync(
        string accountId,
        MoongateHttpUpdateUserRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var parsed = ParseAccountIdOrNull(accountId);
        if (!parsed.HasValue)
        {
            return MoongateHttpOperationResult<MoongateHttpUser>.BadRequest("invalid accountId");
        }

        if (
            request.Username is null &&
            request.Password is null &&
            request.Email is null &&
            request.Role is null &&
            request.IsLocked is null
        )
        {
            return MoongateHttpOperationResult<MoongateHttpUser>.BadRequest("at least one field must be provided");
        }

        AccountType? role = null;
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!Enum.TryParse<AccountType>(request.Role, true, out var parsedRole))
            {
                return MoongateHttpOperationResult<MoongateHttpUser>.BadRequest("invalid role");
            }

            role = parsedRole;
        }

        var updated = await _accountService.UpdateAccountAsync(
                          parsed.Value,
                          request.Username,
                          request.Password,
                          request.Email,
                          role,
                          request.IsLocked,
                          cancellationToken
                      );

        if (updated is null)
        {
            return MoongateHttpOperationResult<MoongateHttpUser>.NotFound();
        }

        return MoongateHttpOperationResult<MoongateHttpUser>.Ok(MapAccountToHttpUser(updated));
    }

    private static MoongateHttpUser MapAccountToHttpUser(UOAccountEntity account)
        => new()
        {
            AccountId = account.Id.Value.ToString(),
            Username = account.Username,
            Email = account.Email,
            Role = account.AccountType.ToString(),
            IsLocked = account.IsLocked,
            CreatedUtc = account.CreatedUtc,
            LastLoginUtc = account.LastLoginUtc,
            CharacterCount = account.CharacterIds.Count
        };

    private static Serial? ParseAccountIdOrNull(string accountId)
        => uint.TryParse(accountId, out var parsedId) ? (Serial)parsedId : null;
}
