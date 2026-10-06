using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;

namespace Moongate.Tests.Integration.Server.Ultima.Services;

/// <summary>
///     The search of the jail by name against PostgreSQL: the in-memory tests compile the same query, which says
///     nothing of how the database reads it.
/// </summary>
[Collection(PostgresTestCollection.Name)]
public sealed class JailServiceTests
{
    [Fact]
    public async Task Find_OnTheWorldDatabase_GivesThePlayersOfTheName_WithoutCase_AndLeavesOutNpcsAndDeletions()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        host.Container.AddPersistenceWorld<MobileEntity>();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobiles = host.Container.Resolve<IDataAccess<MobileEntity>>();
        var mario = new Serial(0x42);
        var luigi = new Serial(0x43);
        await mobiles.UpsertAsync(new MobileEntity { Name = "Pippo", AccountId = mario, Slot = 0 });
        await mobiles.UpsertAsync(new MobileEntity { Name = "PIPPO", AccountId = luigi, Slot = 0 });
        await mobiles.UpsertAsync(new MobileEntity { Name = "Pippolo", AccountId = luigi, Slot = 1 });
        // An NPC of the name, and a player who asked to be deleted.
        await mobiles.UpsertAsync(new MobileEntity { Name = "Pippo" });
        await mobiles.UpsertAsync(
            new MobileEntity
            {
                Name = "Pippo", AccountId = mario, DeletionRequestedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
        var accounts = new RecordingDataAccess<AccountEntity>();
        accounts.Upserted.Add(new AccountEntity { Id = mario, Username = "mario", AccountType = AccountType.Regular });
        accounts.Upserted.Add(new AccountEntity { Id = luigi, Username = "luigi", AccountType = AccountType.GameMaster });

        var found = await Create(mobiles, accounts).FindAsync(" pippo ");

        Assert.Equal(
            [("Pippo", "mario", AccountType.Regular), ("PIPPO", "luigi", AccountType.GameMaster)],
            found.Select(candidate => (candidate.Name, candidate.Account, candidate.AccountType))
        );
    }

    // The search reads the two tables only: what the jail needs to hold prisoners is not there.
    private static JailService Create(IDataAccess<MobileEntity> mobiles, IDataAccess<AccountEntity> accounts)
    {
        var items = TestItems.Create();

        return new JailService(
            new StubDataLoaderService(),
            new RecordingDataAccess<JailSentenceEntity>(),
            mobiles,
            accounts,
            new MobileService(new StubMovementService(), TestSectors.Create()),
            null!,
            new RecordingTeleportService(),
            new RecordingSpeechService(),
            new RecordingTimerService(),
            new JailConfig(),
            new ItemsConfig(),
            items,
            null!,
            new RecordingWorldViewService(),
            new SettableClock(),
            null!
        );
    }
}
