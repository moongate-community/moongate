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
    public async Task SaveAllAsync_LiveSources_PersistsCurrentValuesAndCheckpointsEveryCollection()
    {
        using var root = new TemporaryPersistenceDirectory();
        var entity = new TestEntity { Id = new Serial(1), Name = "before" };
        List<TestEntity> live = [entity];
        var other = new OtherTestEntity { Id = new Serial(2), Value = 42 };
        await using (var owner = new MoongatePersistenceService(root.Path))
        {
            var items = owner.Register("items", () => live);
            owner.Register("others", () => new[] { other });
            await owner.InitializeAsync();
            await items.UpsertAsync(entity);
            entity.Name = "after";
            live.Add(new TestEntity { Id = new Serial(3), Name = "new" });

            await owner.SaveAllAsync();

            Assert.Equal("after", items.GetById(new Serial(1))!.Name);
            Assert.Equal("new", items.GetById(new Serial(3))!.Name);
            Assert.Equal(100, new FileInfo(Path.Combine(root.Path, "items.journal.bin")).Length);
            Assert.Equal(100, new FileInfo(Path.Combine(root.Path, "others.journal.bin")).Length);
        }

        await using var reopened = new MoongatePersistenceService(root.Path);
        var restored = reopened.Register<TestEntity>("items");
        var others = reopened.Register<OtherTestEntity>("others");
        await reopened.InitializeAsync();
        Assert.Equal("after", restored.GetById(new Serial(1))!.Name);
        Assert.Equal(42, others.GetById(new Serial(2))!.Value);
    }

    [Fact]
    public async Task SaveAllAsync_SourceIsFreshEachTime_AbsenceDoesNotDeleteStoredEntities()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        List<TestEntity> live = [];
        var calls = 0;
        var items = owner.Register("items", () => { calls++; return live; });
        Assert.Equal(0, calls);
        await owner.InitializeAsync();
        Assert.Equal(0, calls);
        live.Add(new TestEntity { Id = new Serial(1), Name = "first" });
        await owner.SaveAllAsync();
        live = [new TestEntity { Id = new Serial(2), Name = "second" }];
        await owner.SaveAllAsync();
        live.Clear();
        await owner.SaveAllAsync();

        Assert.Equal(3, calls);
        Assert.Equal("first", items.GetById(new Serial(1))!.Name);
        Assert.Equal("second", items.GetById(new Serial(2))!.Name);
    }

    [Fact]
    public async Task SaveAllAsync_WithoutSource_CheckpointsExplicitUpserts()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var items = owner.Register<TestEntity>("items");
        await owner.InitializeAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "explicit" });

        await owner.SaveAllAsync();

        Assert.Equal("explicit", items.GetById(new Serial(1))!.Name);
        Assert.Equal(100, new FileInfo(Path.Combine(root.Path, "items.journal.bin")).Length);
    }

    [Theory,
     InlineData("null-source"),
     InlineData("null-entity"),
     InlineData("zero-id"),
     InlineData("duplicate-id"),
     InlineData("enumeration")]
    public async Task SaveAllAsync_InvalidSource_FailsBeforeWritingItsCollectionAndCanRetry(string failure)
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var valid = false;
        var items = owner.Register<TestEntity>("items", () => failure == "null-source" && !valid ? null! : Source());
        await owner.InitializeAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "original" });

        await Assert.ThrowsAnyAsync<Exception>(() => owner.SaveAllAsync());
        Assert.Equal("original", items.GetById(new Serial(1))!.Name);
        Assert.Single(items.GetAll());

        valid = true;
        await owner.SaveAllAsync();
        Assert.Equal("changed", items.GetById(new Serial(1))!.Name);

        IEnumerable<TestEntity> Source()
        {
            yield return new TestEntity { Id = new Serial(1), Name = "changed" };
            if (valid)
            {
                yield break;
            }

            switch (failure)
            {
                case "null-entity": yield return null!; break;
                case "zero-id": yield return new TestEntity(); break;
                case "duplicate-id": yield return new TestEntity { Id = new Serial(1) }; break;
                case "enumeration": throw new InvalidOperationException("Source enumeration failed.");
            }
        }
    }

    [Fact]
    public async Task SaveAllAsync_CancellationBeforeCaptureOrDuringEnumeration_DoesNotWrite()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var cancellation = new CancellationTokenSource();
        await using var owner = new MoongatePersistenceService(root.Path);
        var calls = 0;
        var items = owner.Register<TestEntity>("items", () => { calls++; return Source(); });
        await owner.InitializeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.SaveAllAsync(new CancellationToken(true)));
        Assert.Equal(0, calls);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.SaveAllAsync(cancellation.Token));
        Assert.Empty(items.GetAll());

        IEnumerable<TestEntity> Source()
        {
            yield return new TestEntity { Id = new Serial(1), Name = "first" };
            cancellation.Cancel();
            yield return new TestEntity { Id = new Serial(2), Name = "second" };
        }
    }

    [Fact]
    public async Task SaveAllAsync_InvalidLifecycle_RejectsWithoutInvokingSource()
    {
        using var root = new TemporaryPersistenceDirectory();
        var owner = new MoongatePersistenceService(root.Path);
        var calls = 0;
        Assert.Throws<ArgumentNullException>(() => owner.Register<TestEntity>("items", null!));
        owner.Register<TestEntity>("items", () => { calls++; return []; });

        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SaveAllAsync());
        await owner.InitializeAsync();
        await owner.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.SaveAllAsync());
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task SaveAllAsync_CheckpointFailure_PropagatesAndStillCheckpointsHealthyCollections()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var items = owner.Register<TestEntity>("items", () =>
            [new TestEntity { Id = new Serial(1), Name = "saved" }]);
        var others = owner.Register<OtherTestEntity>("others", () =>
            [new OtherTestEntity { Id = new Serial(2), Value = 42 }]);
        await owner.InitializeAsync();
        ReplaceFileWithDirectory(root.Path, "items.snapshot.bin");

        await Assert.ThrowsAnyAsync<IOException>(() => owner.SaveAllAsync());

        Assert.Throws<InvalidOperationException>(() => items.GetAll());
        Assert.Equal(42, others.GetById(new Serial(2))!.Value);
        Assert.Equal(100, new FileInfo(Path.Combine(root.Path, "others.journal.bin")).Length);
    }

    [Fact]
    public async Task CheckpointAsync_CanceledAcrossMultipleCollections_PreservesCancellation()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        owner.Register<TestEntity>("items");
        owner.Register<OtherTestEntity>("others");
        await owner.InitializeAsync();

        var checkpoint = owner.CheckpointAsync(new CancellationToken(true));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checkpoint);
        Assert.True(checkpoint.IsCanceled);
    }

    [Fact]
    public async Task SaveAllAsync_ConcurrentCallsAndCancellation_DoNotOverlapSources()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        await using var owner = new MoongatePersistenceService(root.Path);
        var calls = 0;
        var items = owner.Register<TestEntity>("items", () =>
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 1)
            {
                entered.SetResult();
                if (!release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException();
                }
            }
            return [new TestEntity { Id = new Serial(1), Name = $"save-{call}" }];
        });
        await owner.InitializeAsync();
        var first = Task.Run(() => owner.SaveAllAsync());
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var canceled = owner.SaveAllAsync(cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
            var second = owner.SaveAllAsync();
            Assert.False(second.IsCompleted);
            Assert.Equal(1, Volatile.Read(ref calls));
            release.Set();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("save-2", items.GetById(new Serial(1))!.Name);
        }
        finally
        {
            release.Set();
            await first;
        }
    }

    [Fact]
    public async Task DisposeAsync_DuringSaveAll_DrainsSaveBeforeClosingCollections()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new MoongatePersistenceService(root.Path);
        owner.Register<TestEntity>("items", () =>
        {
            entered.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException();
            }

            return [new TestEntity { Id = new Serial(1), Name = "saved" }];
        });
        await owner.InitializeAsync();
        var save = Task.Run(() => owner.SaveAllAsync());
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var disposal = owner.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
            release.Set();
            await Task.WhenAll(save, disposal).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            release.Set();
            await save;
            await owner.DisposeAsync();
        }

        await using var reopened = new MoongatePersistenceService(root.Path);
        var items = reopened.Register<TestEntity>("items");
        await reopened.InitializeAsync();
        Assert.Equal("saved", items.GetById(new Serial(1))!.Name);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task SaveAllAsync_SourceReentersSaveOrDisposal_RejectsWithoutDeadlock(bool dispose)
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var reenter = true;
        owner.Register<TestEntity>("items", () =>
        {
            if (reenter)
            {
                var task = dispose ? owner.DisposeAsync().AsTask() : owner.SaveAllAsync();
                task.GetAwaiter().GetResult();
            }
            return [];
        });
        await owner.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.Run(() => owner.SaveAllAsync()).WaitAsync(TimeSpan.FromSeconds(10)));
        reenter = false;
        await owner.SaveAllAsync();
    }

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

    [Theory, InlineData(false), InlineData(true)]
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
