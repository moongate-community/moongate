using Moongate.Core.Data;
using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.Core.Utils;
using Moongate.UO.Data.Events.Accounts;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

public class AccountService : IAccountService
{
    private readonly ILogger _logger = Log.ForContext<AccountService>();

    private readonly IEventBusService _eventBusService;
    private const string accountsFilePath = "accounts.mga";
    private readonly Dictionary<string, UOAccountEntity> _accounts = new();
    private readonly IEntityFileService _entityFileService;

    public AccountService(IEntityFileService entityFileService, IEventBusService eventBusService)
    {
        _entityFileService = entityFileService;
        _eventBusService = eventBusService;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await LoadAccountAsync();

        if (_accounts.Count == 0)
        {
            CreateAccount("admin", "admin", AccountLevelType.Admin);
            await SaveAccountsAsync();
        }
    }

    private Task SaveAccountsAsync()
    {
        return _entityFileService.SaveEntitiesAsync(accountsFilePath, _accounts.Values);
    }

    private async Task LoadAccountAsync()
    {
        _accounts.Clear();

        var accounts = await _entityFileService.LoadEntitiesAsync<UOAccountEntity>(accountsFilePath);

        foreach (var account in accounts)
        {
            _accounts[account.Id] = account;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("Saving {Count} accounts to file...", _accounts.Count);
        await SaveAccountsAsync();
    }

    public async Task<string> CreateAccount(
        string username, string password, AccountLevelType accountLevel = AccountLevelType.User
    )
    {
        var account = new UOAccountEntity
        {
            Username = username,
            HashedPassword = HashUtils.CreatePassword(password),
            AccountLevel = accountLevel,
            IsActive = true
        };

        _accounts[account.Id] = account;

        await _eventBusService.PublishAsync(new AccountCreatedEvent(account.Id, username, accountLevel));
        _logger.Information("Account created: {Username} with ID: {AccountId}", username, account.Id);


        return account.Id;
    }

    public async Task<bool> ChangePassword(string accountName, string newPassword)
    {
        var account =
            _accounts.Values.FirstOrDefault(a => a.Username.Equals(accountName, StringComparison.OrdinalIgnoreCase));
        if (account == null)
        {
            _logger.Warning("Account not found: {AccountName}", accountName);
            return false;
        }

        account.HashedPassword = newPassword;
        _logger.Information("Password changed for account: {AccountName}", accountName);

        await SaveAccountsAsync();

        return true;
    }

    public async Task<bool> ChangeLevel(string accountName, AccountLevelType levelType)
    {
        var account =
            _accounts.Values.FirstOrDefault(a => a.Username.Equals(accountName, StringComparison.OrdinalIgnoreCase));

        if (account == null)
        {
            return false;
        }

        account.AccountLevel = levelType;


        await SaveAccountsAsync();
        await _eventBusService.PublishAsync(new AccountLevelChangedEvent(accountName, levelType));
        return true;
    }

    public Task<UOAccountEntity> GetAccountByIdAsync(string accountId)
    {

        if (_accounts.TryGetValue(accountId, out var account))
        {
            return Task.FromResult(account);
        }

        _logger.Warning("Account not found: {AccountId}", accountId);
        return Task.FromResult<UOAccountEntity>(null);
    }

    public async Task<Result<UOAccountEntity>> LoginAsync(string username, string password)
    {
        var account = _accounts.Values.FirstOrDefault(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (account == null || !HashUtils.VerifyPassword(password, account.HashedPassword))
        {
            return Result<UOAccountEntity>.Failure("Username and/or password are invalid");
        }

        await _eventBusService.PublishAsync(new AccountLoginEvent(account.Id, username));
        return Result<UOAccountEntity>.Success(account);
    }

    public void Dispose()
    {
    }

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        return LoadAccountAsync();
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        return SaveAccountsAsync();
    }
}
