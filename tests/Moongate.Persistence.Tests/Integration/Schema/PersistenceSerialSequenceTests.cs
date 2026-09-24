using Moongate.Core.Primitives;
using Moongate.Persistence.Services;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.Integration.Schema;

[Collection(PostgreSqlCollection.Name)]
public sealed class PersistenceSerialSequenceTests
{
    private readonly PostgreSqlFixture _postgres;

    public PersistenceSerialSequenceTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task PreviewAndSynchronize_RenamedIdColumn_PreserveOwnedSequenceAndReservations()
    {
        await using var database = await _postgres.CreateDatabaseAsync();

        await using (var original = FacadeFixture.Create(database))
        {
            var store = original.RegisterEntity<CharacterEntity>();
            original.RegisterEntity<InventoryEntity>();
            await original.InitializeAsync();
            await store.UpsertAsync(new() { Id = new(42), Name = "existing" });
            await database.ExecuteAsync("SELECT setval('plugin_characters.characters_id_seq', 50, true)");
        }

        await using var renamed = new MoongatePersistenceService(
            new([new(PersistenceDatabaseTarget.Realm, database.ConnectionString)], true)
        );
        var renamedStore = renamed.RegisterEntity<RenamedSerialEntity>(target: PersistenceDatabaseTarget.Realm);
        var preview = Assert.Single(await renamed.PreviewSchemaAsync());
        Assert.Contains("RENAME", preview.Ddl);
        Assert.DoesNotContain("CREATE SEQUENCE", preview.Ddl);
        await renamed.InitializeAsync();
        Assert.Equal("existing", (await renamedStore.GetByIdAsync(new(42)))!.Name);
        var entity = new RenamedSerialEntity { Name = "after rename" };
        await renamedStore.UpsertAsync(entity);
        Assert.Equal(new(51), entity.Id);
        Assert.Empty(await renamed.PreviewSchemaAsync());
    }

    [Fact]
    public async Task InitializeAsync_ExistingRows_SeedsAboveMaximumAndDoesNotResetReservations()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await database.ExecuteAsync(
            "CREATE SCHEMA plugin_characters; CREATE TABLE plugin_characters.characters " +
            "(id bigint PRIMARY KEY, name varchar(160)); INSERT INTO plugin_characters.characters VALUES (42, 'existing');"
        );
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        var entity = new CharacterEntity();
        await store.UpsertAsync(entity);
        Assert.Equal(new(43), entity.Id);
        await store.DeleteAsync(entity.Id);
        await owner.InitializeAsync();
        var next = new CharacterEntity();
        await store.UpsertAsync(next);
        Assert.Equal(new(44), next.Id);
        Assert.Empty(await owner.PreviewSchemaAsync());
    }

    [Fact]
    public async Task UpsertAsync_ExhaustedSequence_DoesNotWrapOrOverwrite()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        await database.ExecuteAsync(
            "SELECT setval(pg_get_serial_sequence('plugin_characters.characters', 'id'), 4294967295, false)"
        );
        var last = new CharacterEntity { Name = "last" };
        await store.UpsertAsync(last);
        Assert.Equal(new(uint.MaxValue), last.Id);
        var exhausted = new CharacterEntity { Name = "exhausted" };
        await Assert.ThrowsAnyAsync<Exception>(() => store.UpsertAsync(exhausted));
        Assert.Equal(Serial.Zero, exhausted.Id);
        Assert.Equal("last", Assert.Single(await store.GetAllAsync()).Name);
    }

    [Fact]
    public async Task InitializeAsync_CyclingOwnedSequence_IsRejected()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        await database.ExecuteAsync("ALTER SEQUENCE plugin_characters.characters_id_seq CYCLE");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InitializeAsync());
        Assert.Contains("NO CYCLE", exception.Message);
    }

    [Fact]
    public async Task InitializeAsync_UnrelatedSequenceWithSameName_IsNotAdoptedOrReset()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await database.ExecuteAsync(
            "CREATE SCHEMA plugin_characters; CREATE SEQUENCE plugin_characters.characters_id_seq START WITH 100;"
        );
        await using var owner = FacadeFixture.Create(database);
        owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await Assert.ThrowsAnyAsync<Exception>(() => owner.InitializeAsync());
        Assert.Equal(100L, await database.ScalarAsync<long>("SELECT nextval('plugin_characters.characters_id_seq')"));
        Assert.Null(await database.ScalarAsync<string>("SELECT to_regclass('plugin_characters.characters')::text"));
    }
}
