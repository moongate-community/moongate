using DryIoc;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.DataAccess;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.Extensions;

[Collection(PostgreSqlCollection.Name)]
public sealed class ContainerPersistenceExtensionsTests
{
    private readonly PostgreSqlFixture _postgres;

    public ContainerPersistenceExtensionsTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task AddPersistenceWorld_BeforeModule_ResolvesOneFacadeAndFreezesBatch()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        using var container = new Container();
        container.RegisterMoongatePersistence(
            new(
                [new(PersistenceDatabaseTarget.Realm, database.ConnectionString)],
                true
            )
        );
        container.AddPersistenceWorld<CharacterEntity>();
        container.AddPersistenceModule<CharacterModule>();
        await using var owner = container.Resolve<MoongatePersistenceService>();
        Assert.Same(container.Resolve<DataAccess<CharacterEntity>>(), container.Resolve<IDataAccess<CharacterEntity>>());
        await owner.InitializeAsync();
        Assert.Empty(await container.Resolve<IDataAccess<CharacterEntity>>().GetAllAsync());
        Assert.Throws<InvalidOperationException>(() => container.AddPersistenceEntity<InventoryEntity>());
        Assert.Throws<InvalidOperationException>(() => container.AddPersistenceModule<CharacterModule>());
    }

    [Fact]
    public async Task AuthAndWorld_WithoutModules_RouteToSeparateDatabasesAndCaptureSnapshots()
    {
        await using var auth = await _postgres.CreateDatabaseAsync();
        await using var world = await _postgres.CreateDatabaseAsync();
        using var container = new Container();
        container.RegisterMoongatePersistence(
            new(
                [
                    new(PersistenceDatabaseTarget.Accounts, auth.ConnectionString),
                    new(PersistenceDatabaseTarget.Realm, world.ConnectionString)
                ],
                true
            )
        );
        var source = new CharacterEntity { Id = new(7), Name = "Mario" };
        container.AddPersistenceAuth<AccountsSharedEntity>();
        container.AddPersistenceWorld<CharacterEntity>(
            () => [source],
            value => new() { Id = value.Id, Name = value.Name }
        );
        await using var owner = container.Resolve<MoongatePersistenceService>();

        await owner.InitializeAsync();
        await container.Resolve<IDataAccess<AccountsSharedEntity>>()
            .UpsertAsync(new() { Id = new(1) });
        await owner.SaveAllAsync();

        Assert.Same(container.Resolve<DataAccess<CharacterEntity>>(), container.Resolve<IDataAccess<CharacterEntity>>());
        Assert.Equal("Mario", (await container.Resolve<IDataAccess<CharacterEntity>>().GetByIdAsync(source.Id))!.Name);
        Assert.Equal(1L, await auth.ScalarAsync<long>("SELECT COUNT(*) FROM plugin_shared.accounts_entities"));
        Assert.False(
            await auth.ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'plugin_characters')")
        );
        Assert.False(
            await world.ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'plugin_shared')")
        );
        Assert.Throws<InvalidOperationException>(() => container.AddPersistenceAuth<RealmSharedEntity>());
    }

    [Fact]
    public async Task AuthSnapshot_DoesNotActivateWorldAndRejectsDuplicateTargetRegistration()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var world = await _postgres.CreateDatabaseAsync();
        using var container = new Container();
        container.RegisterMoongatePersistence(
            new(
                [
                    new(PersistenceDatabaseTarget.Accounts, database.ConnectionString),
                    new(PersistenceDatabaseTarget.Realm, world.ConnectionString)
                ],
                true
            )
        );
        container.AddPersistenceAuth<AccountsSharedEntity>(
            () => [new() { Id = new(1) }],
            value => new() { Id = value.Id }
        );
        Assert.Throws<InvalidOperationException>(() => container.AddPersistenceWorld<AccountsSharedEntity>());
        await using var owner = container.Resolve<MoongatePersistenceService>();
        await owner.InitializeAsync();
        await owner.SaveAllAsync();
        Assert.Single(await container.Resolve<IDataAccess<AccountsSharedEntity>>().GetAllAsync());
        Assert.False(
            await world.ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'plugin_shared')")
        );
    }

    [Fact]
    public async Task Auth_ConflictsWithExplicitWorldModule_RejectsBeforeDdl()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        using var container = new Container();
        container.RegisterMoongatePersistence(
            new(
                [
                    new(PersistenceDatabaseTarget.Accounts, database.ConnectionString),
                    new(PersistenceDatabaseTarget.Realm, database.ConnectionString)
                ],
                true
            )
        );
        container.AddPersistenceAuth<CharacterEntity>().AddPersistenceModule<CharacterModule>();
        await using var owner = container.Resolve<MoongatePersistenceService>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InitializeAsync());
        Assert.Contains("Accounts", error.Message);
        Assert.Contains("Realm", error.Message);
        Assert.False(
            await database.ScalarAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'plugin_characters')"
            )
        );
    }

    [Fact]
    public async Task Registration_DuplicatesAndMissingOwnership_FailClearlyBeforeDdl()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        using var container = new Container();
        var options = new PostgreSqlPersistenceOptions(
            [new(PersistenceDatabaseTarget.Realm, database.ConnectionString)],
            true
        );
        container.RegisterMoongatePersistence(options);
        container.AddPersistenceEntity<CharacterEntity>();
        Assert.Throws<InvalidOperationException>(() => container.AddPersistenceEntity<CharacterEntity>());
        Assert.Throws<InvalidOperationException>(() => container.RegisterMoongatePersistence(options));
        await using var owner = container.Resolve<MoongatePersistenceService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InitializeAsync());
        Assert.False(
            await database.ScalarAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'plugin_characters')"
            )
        );
    }

    [Fact]
    public async Task World_EntitiesSharingSchema_UseOneAutomaticModule()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        using var container = new Container();
        container.RegisterMoongatePersistence(
            new(
                [
                    new(PersistenceDatabaseTarget.Realm, database.ConnectionString)
                ],
                true
            )
        );
        container.AddPersistenceWorld<AccountsSharedEntity>().AddPersistenceWorld<RealmSharedEntity>();
        await using var owner = container.Resolve<MoongatePersistenceService>();
        var preview = await owner.PreviewSchemaAsync();
        Assert.Single(preview);
        await owner.InitializeAsync();
        Assert.Empty(await container.Resolve<IDataAccess<AccountsSharedEntity>>().GetAllAsync());
        Assert.Empty(await container.Resolve<IDataAccess<RealmSharedEntity>>().GetAllAsync());
    }
}
