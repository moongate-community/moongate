using Moongate.Core.Primitives;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.Integration.DataAccess;

[Collection(PostgreSqlCollection.Name)]
public sealed class DataAccessTests
{
    private readonly PostgreSqlFixture _postgres;

    public DataAccessTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task UpsertAsync_ZeroId_AssignsIdentityAndUpdatesSameRow()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        var entity = new CharacterEntity { Name = "new" };
        await store.UpsertAsync(entity);
        Assert.True(entity.Id.IsValid);
        var id = entity.Id;
        entity.Name = "updated";
        await store.UpsertAsync(entity);
        Assert.Equal(id, entity.Id);
        Assert.Equal("updated", Assert.Single(await store.GetAllAsync()).Name);
    }

    [Fact]
    public async Task UpsertAsync_AutomaticId_DoesNotOverwriteExplicitIdentity()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        await store.UpsertAsync(new() { Id = new(1), Name = "explicit" });
        var entity = new CharacterEntity { Name = "automatic" };
        await store.UpsertAsync(entity);
        Assert.NotEqual(new(1), entity.Id);
        Assert.Equal("explicit", (await store.GetByIdAsync(new(1)))!.Name);
        Assert.Equal(2, (await store.GetAllAsync()).Count);
    }

    [Fact]
    public async Task UpsertAsync_AutomaticIds_AreUniqueAcrossOwnersAndRestart()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        var entities = Enumerable.Range(0, 24).Select(i => new CharacterEntity { Name = $"entity-{i}" }).ToArray();

        await using (var first = FacadeFixture.Create(database))
        {
            await using (var second = FacadeFixture.Create(database))
            {
                var firstStore = first.RegisterEntity<CharacterEntity>();
                first.RegisterEntity<InventoryEntity>();
                var secondStore = second.RegisterEntity<CharacterEntity>();
                second.RegisterEntity<InventoryEntity>();
                await first.InitializeAsync();
                await second.InitializeAsync();
                await Task.WhenAll(
                    entities.Select((entity, i) => (i % 2 == 0 ? firstStore : secondStore).UpsertAsync(entity))
                );
                Assert.Equal(entities.Length, entities.Select(entity => entity.Id).Distinct().Count());
                Assert.All(entities, entity => Assert.True(entity.Id.IsValid));
            }
        }

        await using var restarted = FacadeFixture.Create(database);
        var store = restarted.RegisterEntity<CharacterEntity>();
        restarted.RegisterEntity<InventoryEntity>();
        await restarted.InitializeAsync();
        var last = new CharacterEntity { Name = "after restart" };
        await store.UpsertAsync(last);
        Assert.DoesNotContain(last.Id, entities.Select(entity => entity.Id));
        Assert.Equal(25, (await store.GetAllAsync()).Count);
    }

    [Fact]
    public async Task UpsertAsync_AutomaticId_RollbackDoesNotReuseSerial()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        var rolledBack = new CharacterEntity();
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteInTransactionAsync(
                PersistenceDatabaseTarget.Realm,
                async transaction =>
                {
                    await transaction.GetDataAccess<CharacterEntity>().UpsertAsync(rolledBack);
                    Assert.True(rolledBack.Id.IsValid);

                    throw new InvalidOperationException("Rollback requested by test");
                }
            )
        );
        Assert.Empty(await store.GetAllAsync());
        var committed = new CharacterEntity();
        await owner.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Realm,
            transaction => transaction.GetDataAccess<CharacterEntity>().UpsertAsync(committed)
        );
        Assert.True(committed.Id.IsValid);
        Assert.NotEqual(rolledBack.Id, committed.Id);
        await store.UpsertAsync(rolledBack);
        Assert.Equal(2, (await store.GetAllAsync()).Count);
    }

    [Fact]
    public async Task UpsertAsync_FailedAutomaticInsert_RestoresZeroAndPreservesExistingRow()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        await database.ExecuteAsync("ALTER TABLE plugin_characters.characters ADD CONSTRAINT unique_name UNIQUE(name)");
        await store.UpsertAsync(new() { Id = new(42), Name = "same" });
        var entity = new CharacterEntity { Name = "same" };
        await Assert.ThrowsAnyAsync<Exception>(() => store.UpsertAsync(entity));
        Assert.Equal(Serial.Zero, entity.Id);
        Assert.Equal(new(42), Assert.Single(await store.GetAllAsync()).Id);
        entity.Name = "different";
        await store.UpsertAsync(entity);
        Assert.True(entity.Id.IsValid);
    }

    [Fact]
    public async Task UpsertAsync_CanceledAutomaticInsert_DoesNotAssignId()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        var entity = new CharacterEntity();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.UpsertAsync(entity, new(true)));
        Assert.Equal(Serial.Zero, entity.Id);
        Assert.Empty(await store.GetAllAsync());
    }

    [Fact]
    public async Task Operations_InvalidArguments_RejectWithoutChangingData()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.GetByIdAsync(Serial.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.DeleteAsync(Serial.Zero));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.UpsertAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.QueryAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.QueryAsync(e => true, -1, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.QueryAsync(e => true, 0, 0));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.UpsertAsync(
                new() { Id = new(1) },
                new(true)
            )
        );
        Assert.Empty(await store.GetAllAsync());
    }

    [Fact]
    public async Task QueryAsync_SerialExpressionsAndPaging_FiltersInDatabase()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();

        foreach (var value in new uint[] { 3, 1, 2 })
        {
            await store.UpsertAsync(new() { Id = new(value), Name = value == 2 ? "O'Brien" : "other" });
        }

        var id = new Serial(1);
        uint captured = 2;
        Assert.Single(await store.QueryAsync(e => e.Id == id));
        Assert.Single(await store.QueryAsync(e => e.Id == new Serial(1)));
        Assert.Single(await store.QueryAsync(e => e.Id == new Serial(captured)));
        Assert.Equal(new(2), Assert.Single(await store.QueryAsync(e => e.Name == "O'Brien")).Id);
        Assert.Equal(new(2), Assert.Single(await store.QueryAsync(e => e.Id > Serial.Zero, 1, 1)).Id);
        Assert.Empty(await store.QueryAsync(e => false));
        await Assert.ThrowsAnyAsync<Exception>(() => store.QueryAsync(e => e.Id == new Serial((uint)e.Name.Length)));
        await Assert.ThrowsAnyAsync<Exception>(() => store.QueryAsync(e => Unsupported(e.Name)));
        Assert.Equal(3, (await store.GetAllAsync()).Count);
        Assert.Equal(3, (await store.QueryAsync(e => true)).Count);
        await Assert.ThrowsAnyAsync<Exception>(() => store.QueryAsync(e => e.Name == "other" && Unsupported(e.Name)));
    }

    [Fact]
    public async Task UpsertAsync_DetachedReadsAndFullRangeIds_PreservesCommittedValues()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();

        foreach (var value in new uint[] { 1, 0x80000000, uint.MaxValue })
        {
            var id = new Serial(value);
            await store.UpsertAsync(new() { Id = id, Name = "before" });
            var copy = await store.GetByIdAsync(id);
            copy!.Name = "not saved";
            Assert.Equal("before", (await store.GetByIdAsync(id))!.Name);
            await store.UpsertAsync(new() { Id = id, Name = "after" });
            Assert.Equal("after", (await store.GetByIdAsync(id))!.Name);
            Assert.True(await store.DeleteAsync(id));
            Assert.False(await store.DeleteAsync(id));
            Assert.Null(await store.GetByIdAsync(id));
        }
    }

    private static bool Unsupported(string value)
    {
        throw new InvalidOperationException("Must not evaluate rows on the client.");
    }
}
