using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Events.Accounts;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Interfaces.Services;

namespace Moongate.Server.Services;

public class AccountService : IAccountService
{
    private readonly IEventBusService _eventBusService;
    private readonly string accountsFilePath = "accounts.mga";
    private readonly Dictionary<string, UOAccountEntity> _accounts = new();
    private readonly IEntityFileService _entityFileService;

    public AccountService(IEntityFileService entityFileService, IEventBusService eventBusService)
    {
        _entityFileService = entityFileService;
        _eventBusService = eventBusService;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _accounts.Clear();

        var accounts = await _entityFileService.LoadEntitiesAsync<UOAccountEntity>(accountsFilePath);

        foreach (var account in accounts)
        {
            _accounts[account.Id] = account;
        }

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

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<string> CreateAccount(
        string username, string password, AccountLevelType accountLevel = AccountLevelType.User
    )
    {
        var account = new UOAccountEntity
        {
            Username = username,
            HashedPassword = password, // In a real application, you should hash the password
            AccountLevel = accountLevel,
            IsActive = true
        };

        _accounts[account.Id] = account;

        await _eventBusService.PublishAsync(new AccountCreatedEvent("", username, accountLevel));


        return "";
    }

    public void Dispose()
    {
    }
}
