using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Services.Accounting;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Accounting;

public sealed class AccountServiceTests
{
    [Test]
    public async Task UpdateAccountAsync_ShouldNotChangeRole_WhenPrivilegeChangesAreNotAllowed()
    {
        using var temp = new TempDirectory();
        using var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var service = new AccountService(persistence);
        var accountId = (Serial)0x00000101;

        await persistence.UnitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = accountId,
                Username = "role-guard-user",
                PasswordHash = "hash",
                AccountType = AccountType.Regular
            }
        );

        var updated = await service.UpdateAccountAsync(accountId, accountType: AccountType.Administrator);
        var persisted = await persistence.UnitOfWork.Accounts.GetByIdAsync(accountId);

        Assert.Multiple(
            () =>
            {
                Assert.That(updated, Is.Not.Null);
                Assert.That(updated!.AccountType, Is.EqualTo(AccountType.Regular));
                Assert.That(persisted, Is.Not.Null);
                Assert.That(persisted!.AccountType, Is.EqualTo(AccountType.Regular));
            }
        );
    }

    [Test]
    public async Task UpdateAccountAsync_ShouldChangeRole_WhenPrivilegeChangesAreAllowed()
    {
        using var temp = new TempDirectory();
        using var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var service = new AccountService(persistence);
        var accountId = (Serial)0x00000102;

        await persistence.UnitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = accountId,
                Username = "role-admin-user",
                PasswordHash = "hash",
                AccountType = AccountType.Regular
            }
        );

        var updated = await service.UpdateAccountAsync(
            accountId,
            accountType: AccountType.Administrator,
            allowPrivilegeChanges: true
        );
        var persisted = await persistence.UnitOfWork.Accounts.GetByIdAsync(accountId);

        Assert.Multiple(
            () =>
            {
                Assert.That(updated, Is.Not.Null);
                Assert.That(updated!.AccountType, Is.EqualTo(AccountType.Administrator));
                Assert.That(persisted, Is.Not.Null);
                Assert.That(persisted!.AccountType, Is.EqualTo(AccountType.Administrator));
            }
        );
    }

    private static async Task<PersistenceService> CreatePersistenceServiceAsync(string rootDirectory)
    {
        var directories = new DirectoriesConfig(rootDirectory, Enum.GetNames<DirectoryType>());
        var persistence = new PersistenceService(
            directories,
            new TimerWheelService(
                new()
                {
                    TickDuration = TimeSpan.FromMilliseconds(250),
                    WheelSize = 512
                }
            ),
            new(),
            new NetworkServiceTestGameEventBusService()
        );

        await persistence.StartAsync();

        return persistence;
    }
}
