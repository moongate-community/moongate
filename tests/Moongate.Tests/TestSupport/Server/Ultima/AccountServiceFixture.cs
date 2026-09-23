using DryIoc;
using Moongate.Core.Utils;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.TestSupport.Server.Ultima;

internal sealed class AccountServiceFixture : IAsyncDisposable
{
    private readonly HostPersistenceFixture _host;
    public IDataAccess<AccountEntity> Accounts { get; }
    public IAccountService Service { get; }
    public MoongatePersistenceService Persistence => _host.Owner;
    public PostgreSqlTestDatabase Database => _host.AccountsDatabase!;
    public PostgreSqlTestDatabase WorldDatabase => _host.Database;
    public string Password { get; } = Guid.NewGuid().ToString("N")[..30];

    private AccountServiceFixture(HostPersistenceFixture host)
    {
        _host = host;
        Accounts = host.Container.Resolve<IDataAccess<AccountEntity>>();
        Service = host.Container.Resolve<IAccountService>();
    }

    public static async Task<AccountServiceFixture> CreateAsync()
    {
        var host = await HostPersistenceFixture.CreateAsync(autoSync: false, twoTargets: true);
        try
        {
            new MoongateUltimaPlugin().Register(host.Container);
            foreach (var migration in Directory.GetFiles(
                             Path.Combine(AppContext.BaseDirectory, "AccountMigrations"),
                             "*.sql"
                         )
                         .Order())
            {
                await host.AccountsDatabase!.ExecuteAsync(await File.ReadAllTextAsync(migration));
            }

            await host.Owner.InitializeAsync();
            return new AccountServiceFixture(host);
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    public async Task<AccountEntity> SeedAsync(bool locked = false)
    {
        var account = new AccountEntity
        {
            Id = new(42), Username = "alice", HashPassword = HashUtils.HashPassword(Password),
            Email = "alice@example.invalid", AccountType = AccountType.GameMaster,
            CreatedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            UpdatedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), IsLocked = locked
        };
        await Accounts.UpsertAsync(account);
        return account;
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
    }
}
