using Moongate.Core.Primitives;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Types.Persistence;
using Npgsql;

namespace Moongate.Persistence.Tests.Integration.Schema;

[Collection(PostgreSqlCollection.Name)]
public sealed class PersistenceSchemaCoordinatorTests
{
    private readonly PostgreSqlFixture _fixture;

    public PersistenceSchemaCoordinatorTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PreviewAsync_FreshModel_ReturnsDdlWithoutChangingCatalog()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var coordinator = CreateCoordinator(database, PersistenceTestModules.Character());

        var changes = await coordinator.PreviewAsync();

        var change = Assert.Single(changes);
        Assert.Equal(PersistenceDatabaseTarget.Realm, change.Target);
        Assert.Equal("plugin.characters", change.ModuleId);
        Assert.Contains("CREATE TABLE", change.Ddl, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0L, await TableCountAsync(database, "plugin_characters", "characters"));
        Assert.False(coordinator.IsReady);
    }

    [Fact]
    public async Task SynchronizeAsync_FreshThenReopenedModel_IsStableAndAutoSyncStaysDisabled()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using (var coordinator = CreateCoordinator(database, PersistenceTestModules.Character()))
        {
            await coordinator.SynchronizeAsync();
            Assert.True(coordinator.IsReady);
            Assert.False(coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm.CodeFirst.IsAutoSyncStructure);
            await coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm
                .Insert(new CharacterEntity { Id = new Serial(1), Name = "seeded" })
                .ExecuteAffrowsAsync();
        }

        await using var reopened = CreateCoordinator(database, PersistenceTestModules.Character());

        Assert.Empty(await reopened.PreviewAsync());
        Assert.Equal("seeded", await database.ScalarAsync<string>(
            "SELECT name FROM plugin_characters.characters WHERE id = 1"));
    }

    [Fact]
    public async Task SynchronizeAsync_TwoSchemasMayUseTheSameTableAndIdNames()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var coordinator = CreateCoordinator(
            database,
            PersistenceTestModules.CharacterShared(),
            PersistenceTestModules.InventoryShared());

        await coordinator.SynchronizeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;
        await orm.Insert(new CharacterSharedEntity { Id = new Serial(7), Name = "character" }).ExecuteAffrowsAsync();
        await orm.Insert(new InventorySharedEntity { Id = new Serial(7), Balance = 42 }).ExecuteAffrowsAsync();

        Assert.Equal("character", await database.ScalarAsync<string>(
            "SELECT name FROM plugin_characters.entities WHERE id = 7"));
        Assert.Equal(42, await database.ScalarAsync<int>(
            "SELECT balance FROM plugin_inventory.entities WHERE id = 7"));
    }

    [Fact]
    public async Task SynchronizeAsync_ExplicitRenameAdditionAndWidening_RetainsSeededRows()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using (var version1 = CreateCoordinator(database, PersistenceTestModules.UpgradeV1()))
        {
            await version1.SynchronizeAsync();
            await version1.GetDatabase(PersistenceDatabaseTarget.Realm).Orm
                .Insert(new UpgradeV1Entity { Id = new Serial(1), Name = "seeded character" })
                .ExecuteAffrowsAsync();
        }

        var before = await database.ScalarAsync<string>(
            "SELECT name FROM plugin_upgrade.characters WHERE id = 1");
        await using (var version2 = CreateCoordinator(database, PersistenceTestModules.UpgradeV2()))
        {
            await version2.SynchronizeAsync();
        }

        Assert.Equal(before, await database.ScalarAsync<string>(
            "SELECT display_name FROM plugin_upgrade.characters WHERE id = 1"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT level FROM plugin_upgrade.characters WHERE id = 1"));

        await using (var version3 = CreateCoordinator(database, PersistenceTestModules.UpgradeV3()))
        {
            await version3.SynchronizeAsync();
        }

        Assert.Equal("seeded character", await database.ScalarAsync<string>(
            "SELECT display_name FROM plugin_upgrade.characters WHERE id = 1"));
        Assert.Equal("bigint", await database.ScalarAsync<string>(
            "SELECT data_type FROM information_schema.columns " +
            "WHERE table_schema = 'plugin_upgrade' AND table_name = 'characters' AND column_name = 'level'"));
        await using var reopened = CreateCoordinator(database, PersistenceTestModules.UpgradeV3());
        Assert.Empty(await reopened.PreviewAsync());
    }

    [Fact]
    public async Task SynchronizeAsync_IntegerSerialStorageInLaterModule_RejectsBatchBeforeAnyDdl()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var invalid = new TestPersistenceModule(
            "plugin.invalid",
            "plugin_invalid",
            PersistenceDatabaseTarget.Realm,
            [typeof(NarrowSqlIdEntity)]);
        await using var coordinator = CreateCoordinator(database, PersistenceTestModules.Character(), invalid);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.SynchronizeAsync());

        Assert.Contains("integer", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0L, await database.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'plugin_characters'"));
        Assert.Equal(0L, await database.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'plugin_invalid'"));
        Assert.False(coordinator.IsReady);
    }

    [Fact]
    public async Task SynchronizeAsync_UnmappedPropertyInLaterModule_RejectsBatchBeforeAnyDdl()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var invalid = new TestPersistenceModule(
            "plugin.mapping",
            "plugin_mapping",
            PersistenceDatabaseTarget.Realm,
            [typeof(UnmappedPropertyEntity)]);
        await using var coordinator = CreateCoordinator(database, PersistenceTestModules.Character(), invalid);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.SynchronizeAsync());

        Assert.Contains("plugin.mapping", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(UnmappedPropertyEntity).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(UnmappedPropertyEntity.Position), exception.Message, StringComparison.Ordinal);
        Assert.Equal(0L, await database.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.schemata " +
            "WHERE schema_name IN ('plugin_characters', 'plugin_mapping')"));
        Assert.False(coordinator.IsReady);
    }

    [Fact]
    public async Task SynchronizeAsync_SupportedMappingWithExplicitIgnoreAndNavigation_PersistsMappedValues()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var mapped = new TestPersistenceModule(
            "plugin.mapping",
            "plugin_mapping",
            PersistenceDatabaseTarget.Realm,
            [typeof(SupportedMappingEntity)]);
        await using var coordinator = CreateCoordinator(database, PersistenceTestModules.Character(), mapped);

        await coordinator.SynchronizeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;
        await orm.Insert(new CharacterEntity { Id = new Serial(7), Name = "linked character" }).ExecuteAffrowsAsync();
        await orm.Insert(new SupportedMappingEntity
        {
            Id = new Serial(1),
            Scores = [12, 34],
            Position = new MappingPosition { X = 123, Y = 456 },
            CharacterId = new Serial(7)
        }).ExecuteAffrowsAsync();

        var saved = await orm.Select<SupportedMappingEntity>().Include(entity => entity.Character).FirstAsync();

        Assert.Equal(new[] { 12, 34 }, saved.Scores);
        Assert.Equal("linked character", saved.Character?.Name);
        Assert.Equal(0L, await database.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.columns " +
            "WHERE table_schema = 'plugin_mapping' AND table_name = 'supported_entities' " +
            "AND column_name IN ('Position', 'position', 'Character', 'character')"));
        Assert.True(coordinator.IsReady);
    }

    [Fact]
    public async Task SynchronizeAsync_TwoCoordinatorsRace_RecompareUnderAdvisoryLock()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var first = CreateCoordinator(database, PersistenceTestModules.Character());
        await using var second = CreateCoordinator(database, PersistenceTestModules.Character());

        await Task.WhenAll(first.SynchronizeAsync(), second.SynchronizeAsync());

        Assert.True(first.IsReady);
        Assert.True(second.IsReady);
        await using var reopened = CreateCoordinator(database, PersistenceTestModules.Character());
        Assert.Empty(await reopened.PreviewAsync());
    }

    [Fact]
    public async Task SynchronizeAsync_CanceledWhileWaitingForLock_ReleasesResourcesAndLeavesCatalogUntouched()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var heldLock = await PostgreSqlSchemaLock.AcquireAsync(
            database.ConnectionString,
            PersistenceDatabaseTarget.Realm,
            CancellationToken.None);
        await using var coordinator = CreateCoordinator(database, PersistenceTestModules.Character());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.SynchronizeAsync(cancellation.Token));

        Assert.False(coordinator.IsReady);
        Assert.Equal(0L, await TableCountAsync(database, "plugin_characters", "characters"));
    }

    [Fact]
    public async Task SynchronizeAsync_FailingDdl_DoesNotClaimReadyAndReleasesLock()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var coordinator = CreateCoordinator(database, PersistenceTestModules.FailingSchema());

        var exception = await Record.ExceptionAsync(() => coordinator.SynchronizeAsync());

        Assert.NotNull(exception);
        Assert.Equal("42704", Assert.IsType<PostgresException>(exception.GetBaseException()).SqlState);
        Assert.False(coordinator.IsReady);
        await using var lockAfterFailure = await PostgreSqlSchemaLock.AcquireAsync(
            database.ConnectionString,
            PersistenceDatabaseTarget.Realm,
            CancellationToken.None);
    }

    [Fact]
    public async Task SynchronizeAsync_DifferentSchemaDatabaseEndpoint_RejectsBeforeDdl()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var mismatched = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        {
            Database = $"moongate_test_{Guid.NewGuid():N}"
        };
        var options = new PostgreSqlPersistenceOptions([
            new PersistenceDatabaseOptions(
                PersistenceDatabaseTarget.Realm,
                database.ConnectionString,
                mismatched.ConnectionString)
        ]);
        var registry = CreateRegistry(PersistenceTestModules.Character());
        await using var coordinator = new PersistenceSchemaCoordinator(options, registry);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.SynchronizeAsync());

        Assert.Contains("same Host, Port and Database", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0L, await TableCountAsync(database, "plugin_characters", "characters"));
    }

    [Fact]
    public async Task InitializeAsync_DefaultPolicyWithChanges_RejectsWithoutDdlAndPreviewRemainsAvailable()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var coordinator = CreateCoordinator(database, PersistenceTestModules.Character());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.InitializeAsync());

        Assert.Contains("schema preview/apply", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(coordinator.IsReady);
        Assert.Single(await coordinator.PreviewAsync());
        Assert.Equal(0L, await TableCountAsync(database, "plugin_characters", "characters"));
    }

    [Fact]
    public async Task InitializeAsync_AutomaticPolicySynchronizesAndBecomesReady()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var options = new PostgreSqlPersistenceOptions(
            [new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, database.ConnectionString)],
            autoSynchronizeSchema: true);
        await using var coordinator = new PersistenceSchemaCoordinator(
            options,
            CreateRegistry(PersistenceTestModules.Character()));

        await coordinator.InitializeAsync();

        Assert.True(coordinator.IsReady);
        Assert.Equal(1L, await TableCountAsync(database, "plugin_characters", "characters"));
    }

    [Fact]
    public async Task GetOwner_AfterValidation_ReturnsTheModuleThatRoutesTheEntityTarget()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var module = PersistenceTestModules.Character();
        await using var coordinator = CreateCoordinator(database, module);
        await coordinator.SynchronizeAsync();

        var owner = coordinator.GetOwner(typeof(CharacterEntity));

        Assert.Same(module, owner);
        Assert.Equal(PersistenceDatabaseTarget.Realm, owner.DatabaseTarget);
    }

    [Fact]
    public async Task SynchronizeAsync_CanceledDuringSynchronousComparison_HoldsLockUntilComparisonSettles()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var coordinator = CreateCoordinator(database, PersistenceTestModules.Character());
        var comparisonEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseComparison = new ManualResetEventSlim();
        coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm.Aop.CommandBefore += (_, _) =>
        {
            comparisonEntered.TrySetResult();
            releaseComparison.Wait();
        };
        using var cancellation = new CancellationTokenSource();
        var synchronization = coordinator.SynchronizeAsync(cancellation.Token);
        await comparisonEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        using var competingCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PostgreSqlSchemaLock.AcquireAsync(
                database.ConnectionString,
                PersistenceDatabaseTarget.Realm,
                competingCancellation.Token));
        }
        finally
        {
            releaseComparison.Set();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => synchronization);
        await using var acquiredAfterSettlement = await PostgreSqlSchemaLock.AcquireAsync(
            database.ConnectionString,
            PersistenceDatabaseTarget.Realm,
            CancellationToken.None);
        Assert.False(coordinator.IsReady);
    }

    private static PersistenceSchemaCoordinator CreateCoordinator(
        PostgreSqlTestDatabase database,
        params TestPersistenceModule[] modules
    )
    {
        var options = new PostgreSqlPersistenceOptions([
            new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, database.ConnectionString)
        ]);

        return new PersistenceSchemaCoordinator(options, CreateRegistry(modules));
    }

    private static PersistenceModuleRegistry CreateRegistry(params TestPersistenceModule[] modules)
    {
        var registry = new PersistenceModuleRegistry();
        foreach (var module in modules)
        {
            registry.RegisterModule(module);
            foreach (var entityType in module.EntityTypes)
            {
                registry.RegisterEntity(entityType);
            }
        }

        return registry;
    }

    private static Task<long> TableCountAsync(PostgreSqlTestDatabase database, string schema, string table)
    {
        return database.ScalarAsync<long>(
            $"SELECT count(*) FROM information_schema.tables WHERE table_schema = '{schema}' AND table_name = '{table}'");
    }
}
