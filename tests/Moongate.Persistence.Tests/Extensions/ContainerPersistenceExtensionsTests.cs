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
    public ContainerPersistenceExtensionsTests(PostgreSqlFixture postgres) { _postgres = postgres; }

    [Fact]
    public async Task AddPersistenceEntity_BeforeModule_ResolvesOneFacadeAndFreezesBatch()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        using var container = new Container();
        container.RegisterMoongatePersistence(new PostgreSqlPersistenceOptions(
            [new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, database.ConnectionString)], true));
        container.AddPersistenceEntity<CharacterEntity>();
        container.AddPersistenceModule<CharacterModule>();
        await using var owner = container.Resolve<MoongatePersistenceService>();
        Assert.Same(container.Resolve<DataAccess<CharacterEntity>>(), container.Resolve<IDataAccess<CharacterEntity>>());
        await owner.InitializeAsync();
        Assert.Empty(await container.Resolve<IDataAccess<CharacterEntity>>().GetAllAsync());
        Assert.Throws<InvalidOperationException>(() => container.AddPersistenceEntity<InventoryEntity>());
        Assert.Throws<InvalidOperationException>(() => container.AddPersistenceModule<CharacterModule>());
    }

    [Fact]
    public async Task Registration_DuplicatesAndMissingOwnership_FailClearlyBeforeDdl()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        using var container = new Container();
        var options = new PostgreSqlPersistenceOptions([new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, database.ConnectionString)], true);
        container.RegisterMoongatePersistence(options);
        container.AddPersistenceEntity<CharacterEntity>();
        Assert.Throws<InvalidOperationException>(() => container.AddPersistenceEntity<CharacterEntity>());
        Assert.Throws<InvalidOperationException>(() => container.RegisterMoongatePersistence(options));
        await using var owner = container.Resolve<MoongatePersistenceService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InitializeAsync());
        Assert.False(await database.ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'plugin_characters')"));
    }
}
