using Moongate.Server.Http.Data;
using Moongate.Server.Http.Data.Results;
using Moongate.Server.Http.Interfaces.Facades;
using Moongate.Server.Interfaces.Services.Accounting;

namespace Moongate.Server.Services.Http.Facades;

/// <summary>
/// Auth facade backed by <see cref="IAccountService" />.
/// </summary>
public sealed class MoongateHttpAuthFacade : IHttpAuthFacade
{
    private readonly Func<IAccountService> _accountServiceResolver;

    public MoongateHttpAuthFacade(Func<IAccountService> accountServiceResolver)
        => _accountServiceResolver = accountServiceResolver;

    public async Task<MoongateHttpOperationResult<MoongateHttpAuthenticatedUser>> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return MoongateHttpOperationResult<MoongateHttpAuthenticatedUser>.BadRequest(
                "username and password are required"
            );
        }

        var accountService = _accountServiceResolver();
        var account = await accountService.LoginAsync(username, password);
        if (account is null)
        {
            return MoongateHttpOperationResult<MoongateHttpAuthenticatedUser>.Unauthorized();
        }

        return MoongateHttpOperationResult<MoongateHttpAuthenticatedUser>.Ok(
            new()
            {
                AccountId = account.Id.Value.ToString(),
                Username = account.Username,
                Role = account.AccountType.ToString()
            }
        );
    }
}
