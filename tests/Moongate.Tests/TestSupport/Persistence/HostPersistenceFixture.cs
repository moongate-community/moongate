using DryIoc;
using Moongate.Core.Directories;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Tests.TestSupport.Persistence;

public sealed class HostPersistenceFixture : IAsyncDisposable
{
    private readonly TemporaryPersistenceDirectory _directory = new();

    public PostgreSqlTestDatabase Database { get; }
    public PostgreSqlTestDatabase? AccountsDatabase { get; }
    public Container Container { get; } = new();
    public MoongatePersistenceService Owner => Container.Resolve<MoongatePersistenceService>();

    private HostPersistenceFixture(PostgreSqlTestDatabase database, PostgreSqlTestDatabase? accountsDatabase, bool autoSync)
    {
        Database = database;
        AccountsDatabase = accountsDatabase;
        List<PersistenceDatabaseOptions> targets = [new(PersistenceDatabaseTarget.Realm, database.ConnectionString)];

        if (accountsDatabase is not null)
        {
            targets.Add(new(PersistenceDatabaseTarget.Accounts, accountsDatabase.ConnectionString));
        }

        // A real host always registers this before a plugin's Register runs (Program.cs does, via
        // services.RegisterInstance(directoriesConfig)); AccountServiceFixture calls
        // MoongateUltimaPlugin.Register directly on this container, which now needs it too.
        Container.RegisterInstance(new DirectoriesConfig(_directory.Path, []));
        Container.RegisterMoongatePersistence(new(targets, autoSync));
    }

    public static async Task<HostPersistenceFixture> CreateAsync(bool autoSync = true, bool twoTargets = false)
    {
        return new(
            await new PostgreSqlFixture().CreateDatabaseAsync(),
            twoTargets ? await new PostgreSqlFixture().CreateDatabaseAsync() : null,
            autoSync
        );
    }

    public void RegisterEntity()
    {
        Container.AddPersistenceModule<TestPersistenceModule>().AddPersistenceEntity<TestEntity>();
    }

    public async ValueTask DisposeAsync()
    {
        if (!Container.IsDisposed)
        {
            await Owner.DisposeAsync();
            Container.Dispose();
        }

        await Database.DisposeAsync();

        if (AccountsDatabase is not null)
        {
            await AccountsDatabase.DisposeAsync();
        }

        _directory.Dispose();
    }
}
