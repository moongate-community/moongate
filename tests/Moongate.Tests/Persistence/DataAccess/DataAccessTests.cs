using Moongate.Core.Primitives;
using Moongate.Persistence.Data;
using Moongate.Persistence.DataAccess;
using Moongate.Persistence.Interfaces.Internal;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Services;
using Moongate.Tests.Support.Persistence;

namespace Moongate.Tests.Persistence.DataAccess;

public sealed class DataAccessTests
{
    [Fact]
    public async Task UpsertGetAndDelete_UseDetachedValuesAndStableIdentity()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var access = owner.Register<TestEntity>("items");
        await owner.InitializeAsync();
        var input = new TestEntity { Id = new Serial(1), Name = "saved" };

        await access.UpsertAsync(input);
        input.Name = "input changed";
        var first = access.GetById(new Serial(1));
        first!.Name = "result changed";

        Assert.Equal("saved", access.GetById(new Serial(1))!.Name);
        Assert.Equal("saved", Assert.Single(access.GetAll()).Name);
        Assert.True(await access.DeleteAsync(new Serial(1)));
        Assert.False(await access.DeleteAsync(new Serial(1)));
        Assert.Null(access.GetById(new Serial(1)));
    }

    [Fact]
    public async Task Operations_NullAndZeroInputs_RejectWithoutChangingCollection()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var access = owner.Register<TestEntity>("items");
        await owner.InitializeAsync();

        await Assert.ThrowsAsync<ArgumentNullException>(() => access.UpsertAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            access.UpsertAsync(new TestEntity { Id = Serial.Zero, Name = "invalid" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => access.GetById(Serial.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => access.DeleteAsync(Serial.Zero));
        await Assert.ThrowsAsync<ArgumentNullException>(() => access.QueryAsync(null!));
        Assert.Empty(access.GetAll());
    }

    [Fact]
    public async Task QueryAsync_FiltersDetachedSnapshotAndSupportsNoMatches()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var access = owner.Register<TestEntity>("items");
        await owner.InitializeAsync();
        await access.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "saved" });
        await access.UpsertAsync(new TestEntity { Id = new Serial(2), Name = "other" });

        var matches = await access.QueryAsync(entity =>
        {
            if (entity.Name != "saved") return false;
            entity.Name = "predicate changed";

            return true;
        });
        var none = await access.QueryAsync(entity => entity.Name == "missing");

        var match = Assert.Single(matches);
        Assert.Equal(new Serial(1), match.Id);
        Assert.Equal("predicate changed", match.Name);
        Assert.Empty(none);
        Assert.Equal("saved", access.GetById(new Serial(1))!.Name);
        match.Name = "result changed";
        Assert.Equal("saved", access.GetById(new Serial(1))!.Name);
    }

    [Fact]
    public async Task QueryAsync_PredicateWrites_DoNotDeadlockOrChangeCapturedView()
    {
        using var root = new TemporaryPersistenceDirectory();
        var owner = new MoongatePersistenceService(root.Path);
        var access = owner.Register<TestEntity>("items");
        await owner.InitializeAsync();
        await access.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "first" });
        await access.UpsertAsync(new TestEntity { Id = new Serial(2), Name = "before" });

        var query = Task.Run(() => access.QueryAsync(entity =>
        {
            if (entity.Id == new Serial(1))
            {
                access.UpsertAsync(new TestEntity { Id = new Serial(2), Name = "after" }).GetAwaiter().GetResult();
                access.UpsertAsync(new TestEntity { Id = new Serial(3), Name = "added" }).GetAwaiter().GetResult();
            }
            return true;
        }));
        var snapshot = await query.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(new[] { "first", "before" }, snapshot.Select(entity => entity.Name));
        Assert.Equal("after", access.GetById(new Serial(2))!.Name);
        Assert.Equal("added", access.GetById(new Serial(3))!.Name);
        await owner.DisposeAsync();
    }

    [Fact]
    public async Task QueryAsync_CancellationAndPredicateFailure_DoNotFaultCollection()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var access = owner.Register<TestEntity>("items");
        await owner.InitializeAsync();
        await access.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "first" });
        await access.UpsertAsync(new TestEntity { Id = new Serial(2), Name = "second" });
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            access.QueryAsync(_ => true, new CancellationToken(true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => access.QueryAsync(_ =>
        {
            cancellation.Cancel();
            return true;
        }, cancellation.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            access.QueryAsync(_ => throw new InvalidOperationException("predicate")));

        Assert.Equal(2, access.GetAll().Count);
        await access.UpsertAsync(new TestEntity { Id = new Serial(3), Name = "healthy" });
        Assert.Equal("healthy", access.GetById(new Serial(3))!.Name);
    }

    [Fact]
    public async Task UpsertAsync_QueuedCall_SerializesBeforeWaiting()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var fileSystem = new FaultingPersistenceFileSystem();
        var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions(), fileSystem);
        var access = new DataAccess<TestEntity>(store);
        await ((IPersistenceCollection)access).InitializeAsync();
        fileSystem.BlockFlush = true;
        var first = Task.Run(() => access.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "blocking" }));
        await fileSystem.FlushEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var input = new TestEntity { Id = new Serial(2), Name = "captured" };

        var queued = access.UpsertAsync(input);
        input.Name = "changed";
        fileSystem.ContinueFlush.Set();
        await first;
        await queued;

        Assert.Equal("captured", access.GetById(new Serial(2))!.Name);
        await ((IAsyncDisposable)access).DisposeAsync();
    }
}
