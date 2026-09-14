using MemoryPack;
using Moongate.Core.Primitives;
using Moongate.Persistence.Data;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Services;
using Moongate.Tests.Support.Persistence;

namespace Moongate.Tests.Persistence.Services;

public sealed class MoongatePersistenceServiceTests
{
    [Fact]
    public async Task Register_ValidCollections_CreateFilesOnlyAtInitializationAndRemainIsolated()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var items = owner.Register<TestEntity>("items");
        var others = owner.Register<OtherTestEntity>("others");

        Assert.Empty(Directory.GetFiles(root.Path));
        await owner.InitializeAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "item" });
        await others.UpsertAsync(new OtherTestEntity { Id = new Serial(1), Value = 42 });

        Assert.Equal(6, Directory.GetFiles(root.Path).Length);
        Assert.Equal("item", items.GetById(new Serial(1))!.Name);
        Assert.Equal(42, others.GetById(new Serial(1))!.Value);
    }

    [Fact]
    public async Task Register_DuplicateNameTypeInvalidNameOrStartedOwner_Rejects()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        owner.Register<TestEntity>("items");

        Assert.Throws<InvalidOperationException>(() => owner.Register<OtherTestEntity>("items"));
        Assert.Throws<InvalidOperationException>(() => owner.Register<TestEntity>("other-name"));
        Assert.Throws<ArgumentException>(() => owner.Register<OtherTestEntity>("Invalid"));
        await owner.InitializeAsync();
        Assert.Throws<InvalidOperationException>(() => owner.Register<OtherTestEntity>("others"));
    }

    [Fact]
    public async Task InitializeAsync_ReopenValidatesAndRestoresTypedPayloads()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using (var first = new MoongatePersistenceService(root.Path))
        {
            var items = first.Register<TestEntity>("items");
            await first.InitializeAsync();
            await items.UpsertAsync(new TestEntity { Id = new Serial(9), Name = "persisted" });
            await first.CheckpointAsync();
        }

        await using var reopened = new MoongatePersistenceService(root.Path);
        var restored = reopened.Register<TestEntity>("items");
        await reopened.InitializeAsync();

        Assert.Equal("persisted", restored.GetById(new Serial(9))!.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializeAsync_InvalidTypedPayload_AbortsEveryOpenedCollectionAndFaultsOwner(bool mismatchedId)
    {
        using var root = new TemporaryPersistenceDirectory();
        await WriteRawAsync(root.Path, "good", new Serial(1),
            MemoryPackSerializer.Serialize(new TestEntity { Id = new Serial(1), Name = "valid" }));
        await WriteRawAsync(root.Path, "bad", new Serial(2), mismatchedId
            ? MemoryPackSerializer.Serialize(new OtherTestEntity { Id = new Serial(3), Value = 1 })
            : new byte[] { 1 });
        await using var owner = new MoongatePersistenceService(root.Path);
        owner.Register<TestEntity>("good");
        owner.Register<OtherTestEntity>("bad");

        await Assert.ThrowsAnyAsync<Exception>(() => owner.InitializeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InitializeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CheckpointAsync());
        Assert.Throws<InvalidOperationException>(() => owner.Register<TestEntity>("later"));

        await using var good = new BinaryCollectionStore(root.Path, "good", new PersistenceOptions());
        await using var bad = new BinaryCollectionStore(root.Path, "bad", new PersistenceOptions());
        await good.InitializeAsync();
        await bad.InitializeAsync();
    }

    [Fact]
    public async Task DisposeAsync_ClosesEveryOwnedCollection()
    {
        using var root = new TemporaryPersistenceDirectory();
        var owner = new MoongatePersistenceService(root.Path);
        var items = owner.Register<TestEntity>("items");
        owner.Register<OtherTestEntity>("others");
        await owner.InitializeAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "saved" });

        await owner.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => items.GetAll());
        await using var itemsStore = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await using var othersStore = new BinaryCollectionStore(root.Path, "others", new PersistenceOptions());
        await itemsStore.InitializeAsync();
        await othersStore.InitializeAsync();
    }

    [Fact]
    public async Task DisposeAsync_MultipleCheckpointFailures_AggregatesAndClosesEveryCollection()
    {
        using var root = new TemporaryPersistenceDirectory();
        var owner = new MoongatePersistenceService(root.Path);
        owner.Register<TestEntity>("items");
        owner.Register<OtherTestEntity>("others");
        await owner.InitializeAsync();
        ReplaceFileWithDirectory(root.Path, "items.snapshot.bin");
        ReplaceFileWithDirectory(root.Path, "others.snapshot.bin");

        var failure = await Assert.ThrowsAsync<AggregateException>(() => owner.DisposeAsync().AsTask());

        Assert.Equal(2, failure.InnerExceptions.Count);
        ResetCollection(root.Path, "items");
        ResetCollection(root.Path, "others");
        await using var itemsStore = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await using var othersStore = new BinaryCollectionStore(root.Path, "others", new PersistenceOptions());
        await itemsStore.InitializeAsync();
        await othersStore.InitializeAsync();
    }

    private static void ReplaceFileWithDirectory(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.Delete(path);
        Directory.CreateDirectory(path);
    }

    private static void ResetCollection(string directory, string name)
    {
        Directory.Delete(Path.Combine(directory, name + ".snapshot.bin"));
        File.Delete(Path.Combine(directory, name + ".journal.bin"));
        File.Delete(Path.Combine(directory, name + ".lock"));
    }

    private static async Task WriteRawAsync(string directory, string name, Serial key, byte[] payload)
    {
        var store = new BinaryCollectionStore(directory, name, new PersistenceOptions());
        await store.InitializeAsync();
        await store.UpsertAsync(key, payload);
        await store.AbortAsync();
    }
}
