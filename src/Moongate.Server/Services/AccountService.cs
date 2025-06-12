using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
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
            CreateAccount("admin", "admin123", AccountLevelType.Admin);
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
            HashedPassword = password,
            AccountLevel = accountLevel,
            IsActive = true
        };

        _accounts[account.Id] = account;

        await _eventBusService.PublishAsync(new AccountCreatedEvent(account.Id, username, accountLevel));
        _logger.Information("Account created: {Username} with ID: {AccountId}", username, account.Id);


        return account.Id;
    }

    public void Dispose()
    {
    }
}
