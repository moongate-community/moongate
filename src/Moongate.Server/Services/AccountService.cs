using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Interfaces.Services;

namespace Moongate.Server.Services;

public class AccountService : IAccountService
{

    private readonly string accountsFilePath = "accounts.mgd";
    private readonly Dictionary<string, UOAccountEntity> _accounts = new();

    private readonly IEntityFileService _entityFileService;

    public AccountService(IEntityFileService entityFileService)
    {
        _entityFileService = entityFileService;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public string CreateAccount(string username, string password, AccountLevelType accountLevel = AccountLevelType.User)
    {
        return "";
    }

    public void Dispose()
    {
    }
}
