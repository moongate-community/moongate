using MemoryPack;
using Moongate.Core.Primitives;
using Moongate.Persistence.Data;
using Moongate.Persistence.DataAccess;
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

    [Fact]
    public async Task SaveAllAsync_CaptureCallback_InvokesCaptureOnceAndCommitsCapturedBytes()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var live = new TestEntity { Id = new Serial(1), Name = "before capture" };
        var sourceCalls = 0;
        var items = owner.Register<TestEntity>("items", () =>
        {
            sourceCalls++;
            return [live];
        });
        await owner.InitializeAsync();
        var callbackCalls = 0;

        await owner.SaveAllAsync((capture, _) =>
        {
            callbackCalls++;
            capture();
            live.Name = "after capture";
            return Task.CompletedTask;
        });

        Assert.Equal(1, callbackCalls);
        Assert.Equal(1, sourceCalls);
        Assert.Equal("before capture", items.GetById(live.Id)!.Name);
    }

    [Fact]
    public async Task SaveAllAsync_SecondSourceFails_DoesNotCommitFirstSource()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var items = owner.Register<TestEntity>("items", () =>
            [new TestEntity { Id = new Serial(1), Name = "changed" }]);
        owner.Register<OtherTestEntity>("others", () => throw new InvalidOperationException("capture failed"));
        await owner.InitializeAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "original" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SaveAllAsync());

        Assert.Equal("original", items.GetById(new Serial(1))!.Name);
    }

    [Fact]
    public async Task SaveAllAsync_QueuedMutations_RunAfterCapturedSave()
    {
        using var root = new TemporaryPersistenceDirectory();
        var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var owner = new MoongatePersistenceService(root.Path);
        var items = owner.Register<TestEntity>("items", () =>
            [new TestEntity { Id = new Serial(1), Name = "captured" }]);
        await owner.InitializeAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(2), Name = "delete me" });
        var save = owner.SaveAllAsync(async (capture, token) =>
        {
            capture();
            captured.SetResult();
            await release.Task.WaitAsync(token);
        });
        try
        {
            await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var queuedEntity = new TestEntity { Id = new Serial(1), Name = "queued" };

            var upsert = items.UpsertAsync(queuedEntity);
            var delete = items.DeleteAsync(new Serial(2));
            queuedEntity.Name = "changed after call";

            Assert.False(upsert.IsCompleted);
            Assert.False(delete.IsCompleted);
            release.TrySetResult();
            await Task.WhenAll(save, upsert, delete).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal("queued", items.GetById(new Serial(1))!.Name);
            Assert.Null(items.GetById(new Serial(2)));
        }
        finally
        {
            release.TrySetResult();
            await save.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public async Task CollectionDisposeAsync_DuringPausedSave_RunsAfterSaveCompletes()
    {
        using var root = new TemporaryPersistenceDirectory();
        var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var owner = new MoongatePersistenceService(root.Path);
        var items = owner.Register<TestEntity>("items", () =>
            [new TestEntity { Id = new Serial(1), Name = "captured" }]);
        await owner.InitializeAsync();
        var save = owner.SaveAllAsync(async (capture, token) =>
        {
            capture();
            captured.SetResult();
            await release.Task.WaitAsync(token);
        });
        Task? disposal = null;

        try
        {
            await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
            disposal = ((IAsyncDisposable)items).DisposeAsync().AsTask();

            Assert.False(disposal.IsCompleted);
            release.TrySetResult();
            await Task.WhenAll(save, disposal).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Throws<ObjectDisposedException>(() => items.GetAll());
        }
        finally
        {
            release.TrySetResult();
            await save.WaitAsync(TimeSpan.FromSeconds(10));
            if (disposal is not null)
            {
                await disposal.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task SaveAllAsync_SourceDisposesCollection_RejectsWithoutClosingCollection(bool separateCaptureContext)
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var reenter = true;
        DataAccess<TestEntity>? items = null;
        items = owner.Register<TestEntity>("items", () =>
        {
            if (reenter)
            {
                var disposal = ((IAsyncDisposable)items!).DisposeAsync();
                Assert.True(disposal.IsCompleted, "Collection disposal did not reject capture reentry.");
                disposal.AsTask().GetAwaiter().GetResult();
            }

            return [new TestEntity { Id = new Serial(1), Name = "saved" }];
        });
        await owner.InitializeAsync();

        var save = owner.SaveAllAsync(async (capture, _) =>
        {
            if (separateCaptureContext)
            {
                Task captureTask;
                using (ExecutionContext.SuppressFlow())
                {
                    captureTask = Task.Run(capture, CancellationToken.None);
                }
                await captureTask;
            }
            else
            {
                capture();
            }
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => save.WaitAsync(TimeSpan.FromSeconds(10)));
        reenter = false;
        await owner.SaveAllAsync();

        Assert.Equal("saved", items.GetById(new Serial(1))!.Name);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task SaveAllAsync_SourceDisposesCollectionDuringOwnerShutdown_RejectsBeforeJoiningOwner(bool separateCaptureContext)
    {
        using var root = new TemporaryPersistenceDirectory();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new MoongatePersistenceService(root.Path);
        DataAccess<TestEntity>? items = null;
        items = owner.Register<TestEntity>("items", () =>
        {
            var failure = Record.Exception(() =>
            {
                var disposal = ((IAsyncDisposable)items!).DisposeAsync();
                Assert.True(disposal.IsCompleted, "Collection disposal tried to join its own capture.");
                disposal.GetAwaiter().GetResult();
            });
            Assert.IsType<InvalidOperationException>(failure);
            return [new TestEntity { Id = new Serial(1), Name = "saved" }];
        });
        await owner.InitializeAsync();
        var save = owner.SaveAllAsync(async (capture, token) =>
        {
            ready.SetResult();
            await release.Task.WaitAsync(token);
            if (separateCaptureContext)
            {
                Task captureTask;
                using (ExecutionContext.SuppressFlow())
                {
                    captureTask = Task.Run(capture, CancellationToken.None);
                }
                await captureTask;
            }
            else
            {
                capture();
            }
        });
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var shutdown = owner.DisposeAsync().AsTask();
            release.TrySetResult();
            await Task.WhenAll(save, shutdown).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(save, owner.DisposeAsync().AsTask()).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task SaveAllAsync_CaptureCallbackFailureOrCancellation_ReleasesMutationGate(bool cancel)
    {
        using var root = new TemporaryPersistenceDirectory();
        using var cancellation = new CancellationTokenSource();
        await using var owner = new MoongatePersistenceService(root.Path);
        var items = owner.Register<TestEntity>("items", () => []);
        await owner.InitializeAsync();

        Task FailCapture(Action capture, CancellationToken token)
        {
            capture();
            if (cancel)
            {
                cancellation.Cancel();
                return Task.FromCanceled(token);
            }

            return Task.FromException(new InvalidOperationException("capture callback failed"));
        }

        var failed = owner.SaveAllAsync(FailCapture, cancellation.Token);
        await Assert.ThrowsAnyAsync<Exception>(() => failed);
        await items.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "after failure" })
                   .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("after failure", items.GetById(new Serial(1))!.Name);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task SaveAllAsync_OmittedOrDuplicateCapture_RejectsBeforeCommit(bool duplicate)
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var items = owner.Register<TestEntity>("items", () =>
            [new TestEntity { Id = new Serial(1), Name = "changed" }]);
        await owner.InitializeAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "original" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SaveAllAsync((capture, _) =>
        {
            if (duplicate)
            {
                capture();
                Assert.Throws<InvalidOperationException>(capture);
            }

            return Task.CompletedTask;
        }));

        Assert.Equal("original", items.GetById(new Serial(1))!.Name);
    }

    [Fact]
    public async Task SaveAllAsync_ConcurrentDuplicateBeforeCallbackReturns_RejectsBeforeCommit()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var items = owner.Register<TestEntity>("items", () =>
            [new TestEntity { Id = new Serial(1), Name = "changed" }]);
        await owner.InitializeAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "original" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SaveAllAsync(async (capture, token) =>
        {
            capture();
            var duplicate = Task.Run(() => Record.Exception(capture), CancellationToken.None);
            var duplicateFailure = await duplicate.WaitAsync(TimeSpan.FromSeconds(10), token);
            Assert.IsType<InvalidOperationException>(duplicateFailure);
        }));

        Assert.Equal("original", items.GetById(new Serial(1))!.Name);
    }

    [Fact]
    public async Task SaveAllAsync_DeferredCaptureAfterCompletion_RejectsWithoutChangingCommittedData()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(root.Path);
        var live = new TestEntity { Id = new Serial(1), Name = "captured" };
        var items = owner.Register<TestEntity>("items", () => [live]);
        await owner.InitializeAsync();
        Action? deferred = null;

        await owner.SaveAllAsync((capture, _) =>
        {
            deferred = capture;
            capture();
            return Task.CompletedTask;
        });
        live.Name = "late";

        Assert.Throws<InvalidOperationException>(deferred!);
        Assert.Equal("captured", items.GetById(new Serial(1))!.Name);
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

    [Theory,
     InlineData("upsert"),
     InlineData("delete"),
     InlineData("checkpoint"),
     InlineData("save"),
     InlineData("dispose")]
    public async Task SaveAllAsync_SourceReentersMutationSaveOrDisposal_RejectsWithoutDeadlock(string operation)
    {
        using var root = new TemporaryPersistenceDirectory();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var owner = new MoongatePersistenceService(root.Path);
        var reenter = true;
        DataAccess<TestEntity>? items = null;
        items = owner.Register<TestEntity>("items", () =>
        {
            if (reenter)
            {
                Task task = operation switch
                {
                    "upsert" => items!.UpsertAsync(
                        new TestEntity { Id = new Serial(1), Name = "reentry" },
                        cancellation.Token
                    ),
                    "delete" => items!.DeleteAsync(new Serial(1), cancellation.Token),
                    "checkpoint" => owner.CheckpointAsync(cancellation.Token),
                    "save" => owner.SaveAllAsync(cancellation.Token),
                    "dispose" => DisposeOwner(),
                    _ => throw new InvalidOperationException(operation)
                };
                task.GetAwaiter().GetResult();
            }
            return [];
        });
        await owner.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.Run(() => owner.SaveAllAsync()).WaitAsync(TimeSpan.FromSeconds(10)));
        reenter = false;
        await owner.SaveAllAsync();

        Task DisposeOwner()
        {
            var disposal = owner.DisposeAsync();
            Assert.True(disposal.IsCompleted, "Owner disposal did not reject capture reentry.");

            return disposal.AsTask();
        }
    }

    [Fact]
    public async Task SaveAllAsync_CaptureRunsWithoutCallerExecutionContext_SourceReentryStillRejectsPromptly()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var owner = new MoongatePersistenceService(root.Path);
        var reenter = true;
        owner.Register<TestEntity>("items", () =>
        {
            if (reenter)
            {
                owner.CheckpointAsync(cancellation.Token).GetAwaiter().GetResult();
            }

            return [];
        });
        await owner.InitializeAsync();

        var failure = owner.SaveAllAsync(async (capture, _) =>
        {
            Task captureTask;
            using (ExecutionContext.SuppressFlow())
            {
                captureTask = Task.Run(capture, CancellationToken.None);
            }

            await captureTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            failure.WaitAsync(TimeSpan.FromSeconds(10)));
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
