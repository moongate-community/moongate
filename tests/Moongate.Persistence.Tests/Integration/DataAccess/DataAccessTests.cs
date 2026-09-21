using Moongate.Core.Primitives;
using Moongate.Persistence.Tests.TestSupport.Persistence;

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
    public async Task Operations_InvalidArguments_RejectWithoutChangingData()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.GetByIdAsync(Serial.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.DeleteAsync(Serial.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.UpsertAsync(new()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.UpsertAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.QueryAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.QueryAsync(e => true, -1, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.QueryAsync(e => true, 0, 0));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.UpsertAsync(
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
        => throw new InvalidOperationException("Must not evaluate rows on the client.");
}
