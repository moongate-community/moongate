using System.Buffers.Binary;
using Moongate.Core.Primitives;
using Moongate.Persistence.Data;
using Moongate.Persistence.Internal;
using Moongate.Tests.Support.Persistence;

namespace Moongate.Tests.Integration.Persistence;

public sealed class BinaryCollectionStoreTests
{
    [Theory]
    [InlineData(100, false)]
    [InlineData(108, true)]
    [InlineData(132, false)]
    [InlineData(120, true)]
    public async Task InitializeAsync_CorruptSnapshotRecord_Rejects(int offset, bool repairCrc)
    {
        using var root = new TemporaryPersistenceDirectory();
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
            await store.UpsertAsync(new Serial(1), [1]);
        }
        var path = System.IO.Path.Combine(root.Path, "items.snapshot.bin");
        var bytes = File.ReadAllBytes(path);
        bytes[offset] ^= 1;
        if (repairCrc)
        {
            PersistenceFixture.Rechecksum(bytes, 100, 28);
        }

        File.WriteAllBytes(path, bytes);
        await using var failed = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await Assert.ThrowsAsync<InvalidDataException>(() => failed.InitializeAsync());
    }

    [Fact]
    public async Task InitializeAsync_JournalBaseAboveSnapshot_Fails()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
        }

        var path = System.IO.Path.Combine(root.Path, "items.journal.bin");
        var bytes = File.ReadAllBytes(path);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16), 1);
        PersistenceFixture.Rechecksum(bytes, 0, 96);
        File.WriteAllBytes(path, bytes);
        await using var failed = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await Assert.ThrowsAsync<InvalidDataException>(() => failed.InitializeAsync());
    }

    [Fact]
    public async Task InitializeAsync_ReplayedReplacementAndDelete_RestoresFinalState()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
            await store.UpsertAsync(new Serial(1), [1]);
            await store.UpsertAsync(new Serial(2), [2]);
            await store.UpsertAsync(new Serial(1), [3]);
            await store.DeleteAsync(new Serial(2));
            await store.AbortAsync();
        }
        await using var reopened = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await reopened.InitializeAsync();
        Assert.Equal(new byte[] { 3 }, reopened.Get(new Serial(1)));
        Assert.Null(reopened.Get(new Serial(2)));
    }

    [Fact]
    public async Task InitializeAsync_InterruptedCreation_FailsClosedAndReleasesLock()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var io = new FaultingPersistenceFileSystem { FailJournalPublication = true };
        var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions(), io);
        await Assert.ThrowsAsync<IOException>(() => store.InitializeAsync());
        await store.DisposeAsync();
        Assert.True(File.Exists(System.IO.Path.Combine(root.Path, "items.snapshot.bin")));
        Assert.False(File.Exists(System.IO.Path.Combine(root.Path, "items.journal.bin")));
        await using var reopened = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await Assert.ThrowsAsync<InvalidDataException>(() => reopened.InitializeAsync());
    }

    [Fact]
    public async Task CheckpointAsync_JournalPublishedButReopenFails_RecoversFromNewPair()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var io = new FaultingPersistenceFileSystem();
        var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions(), io);
        await store.InitializeAsync();
        await store.UpsertAsync(new Serial(1), [1]);
        io.FailJournalOpen = true;
        await Assert.ThrowsAsync<IOException>(() => store.CheckpointAsync());
        await store.DisposeAsync();
        await using var reopened = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await reopened.InitializeAsync();
        Assert.Equal(new byte[] { 1 }, reopened.Get(new Serial(1)));
        await reopened.UpsertAsync(new Serial(2), [2]);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(133)]
    public async Task InitializeAsync_SnapshotTruncatedOrHasTrailingData_Fails(int length)
    {
        using var root = new TemporaryPersistenceDirectory();
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
        }

        using (var stream = File.OpenWrite(System.IO.Path.Combine(root.Path, "items.snapshot.bin")))
        {
            stream.SetLength(length);
        }

        await using var failed = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await Assert.ThrowsAsync<InvalidDataException>(() => failed.InitializeAsync());
    }

    [Fact]
    public async Task InitializeAsync_CancelledBeforeWork_DoesNotCreateFilesAndCanRetry()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.InitializeAsync(new CancellationToken(true)));
        Assert.Empty(Directory.GetFiles(root.Path));
        await store.InitializeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CheckpointAsync(new CancellationToken(true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DeleteAsync(new Serial(1), new CancellationToken(true)));
        Assert.Empty(store.Capture());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpsertAsync_UncertainWrite_FaultsUntilRecovery(bool failFlush)
    {
        using var root = new TemporaryPersistenceDirectory();
        using var io = new FaultingPersistenceFileSystem();
        var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions(), io);
        await store.InitializeAsync();
        await store.UpsertAsync(new Serial(1), [1]);
        io.FailWrite = !failFlush;
        io.FailFlush = failFlush;
        await Assert.ThrowsAsync<IOException>(() => store.UpsertAsync(new Serial(2), [2]));
        Assert.Throws<InvalidOperationException>(() => store.Get(new Serial(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.DeleteAsync(new Serial(1)));
        await store.DisposeAsync();
        await using var reopened = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await reopened.InitializeAsync();
        Assert.Equal(new byte[] { 1 }, reopened.Get(new Serial(1)));
        Assert.Equal(failFlush ? new byte[] { 2 } : null, reopened.Get(new Serial(2)));
    }

    [Fact]
    public async Task DisposeAsync_FailedCheckpoint_ReleasesLockAndReportsFailure()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var io = new FaultingPersistenceFileSystem();
        var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions(), io);
        await store.InitializeAsync();
        await store.UpsertAsync(new Serial(1), [1]);
        io.FailJournalPublication = true;
        await Assert.ThrowsAsync<IOException>(() => store.DisposeAsync().AsTask());
        await using var reopened = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await reopened.InitializeAsync();
        Assert.Equal(new byte[] { 1 }, reopened.Get(new Serial(1)));
        await reopened.UpsertAsync(new Serial(2), [2]);
    }

    [Fact]
    public async Task UpsertAsync_AutomaticCheckpointFails_DoesNotAppendNextMutation()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var io = new FaultingPersistenceFileSystem();
        var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions { JournalCheckpointThresholdBytes = 133 }, io);
        await store.InitializeAsync();
        await store.UpsertAsync(new Serial(1), [1]);
        io.FailJournalPublication = true;
        await Assert.ThrowsAsync<IOException>(() => store.UpsertAsync(new Serial(2), [2]));
        await store.DisposeAsync();
        await using var reopened = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await reopened.InitializeAsync();
        Assert.Equal(new byte[] { 1 }, reopened.Get(new Serial(1)));
        Assert.Null(reopened.Get(new Serial(2)));
    }

    [Fact]
    public async Task DisposeAsync_QueuedMutation_RejectsItAndDrainsStartedCommit()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var io = new FaultingPersistenceFileSystem();
        var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions(), io);
        await store.InitializeAsync();
        io.BlockFlush = true;
        var first = Task.Run(() => store.UpsertAsync(new Serial(1), [1]));
        await io.FlushEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var queued = store.UpsertAsync(new Serial(2), [2]);
        var disposal = store.DisposeAsync().AsTask();
        try
        {
            Assert.False(disposal.IsCompleted);
            Assert.Throws<ObjectDisposedException>(() => store.Capture());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => store.UpsertAsync(new Serial(3), [3]));
        }
        finally { io.ContinueFlush.Set(); }
        await first;
        await Assert.ThrowsAsync<ObjectDisposedException>(() => queued);
        await disposal;
        await using var reopened = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await reopened.InitializeAsync();
        Assert.Equal(new byte[] { 1 }, reopened.Get(new Serial(1)));
        Assert.Null(reopened.Get(new Serial(2)));
        Assert.Null(reopened.Get(new Serial(3)));
    }

    [Fact]
    public async Task UpsertAsync_WaitingForCommit_CopiesInputBeforeAwaitAndHonorsQueuedCancellation()
    {
        using var root = new TemporaryPersistenceDirectory();
        using var io = new FaultingPersistenceFileSystem();
        await using var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions(), io);
        await store.InitializeAsync();
        io.BlockFlush = true;
        using var cancellation = new CancellationTokenSource();
        var first = Task.Run(() => store.UpsertAsync(new Serial(1), [1], cancellation.Token));
        await io.FlushEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        byte[] input = [2];
        var queued = store.UpsertAsync(new Serial(2), input);
        input[0] = 9;
        var cancelled = store.UpsertAsync(new Serial(3), [3], cancellation.Token);
        var cancelledCapture = store.CaptureAsync(cancellation.Token);
        cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledCapture);
        }
        finally { io.ContinueFlush.Set(); }
        await first;
        await queued;
        Assert.Equal(new byte[] { 1 }, store.Get(new Serial(1)));
        Assert.Equal(new byte[] { 2 }, store.Get(new Serial(2)));
        Assert.Null(store.Get(new Serial(3)));
    }

    [Theory]
    [InlineData("items.snapshot.bin", 0, false)]
    [InlineData("items.snapshot.bin", 8, true)]
    [InlineData("items.snapshot.bin", 32, true)]
    [InlineData("items.journal.bin", 0, false)]
    [InlineData("items.journal.bin", 8, true)]
    [InlineData("items.journal.bin", 132, false)]
    [InlineData("items.journal.bin", 120, false)]
    [InlineData("items.journal.bin", 104, true)]
    [InlineData("items.journal.bin", 108, true)]
    [InlineData("items.journal.bin", 116, true)]
    [InlineData("items.journal.bin", 120, true)]
    public async Task InitializeAsync_CompleteCorruption_FailsVisibly(string file, int offset, bool repairCrc)
    {
        using var root = new TemporaryPersistenceDirectory();
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
            await store.UpsertAsync(new Serial(1), [1]);
            await store.AbortAsync();
        }
        var path = System.IO.Path.Combine(root.Path, file);
        var bytes = File.ReadAllBytes(path);
        bytes[offset] = offset == 116 ? (byte)0 : (byte)(bytes[offset] ^ 0x80);
        if (repairCrc)
        {
            PersistenceFixture.Rechecksum(bytes, offset < 100 ? 0 : 100, offset < 100 ? 96 : 28);
        }

        File.WriteAllBytes(path, bytes);
        await using var failed = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions { MaxPayloadBytes = 64 });
        await Assert.ThrowsAsync<InvalidDataException>(() => failed.InitializeAsync());
    }

    [Theory]
    [InlineData("items.snapshot.bin", 0)]
    [InlineData("items.snapshot.bin", 99)]
    [InlineData("items.journal.bin", 99)]
    public async Task InitializeAsync_IncompleteFileHeader_Fails(string file, int length)
    {
        using var root = new TemporaryPersistenceDirectory();
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
        }

        using (var stream = File.OpenWrite(System.IO.Path.Combine(root.Path, file)))
        {
            stream.SetLength(length);
        }

        await using var failed = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await Assert.ThrowsAsync<InvalidDataException>(() => failed.InitializeAsync());
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(3UL)]
    public async Task InitializeAsync_DuplicateOrGappedSequence_Fails(ulong sequence)
    {
        using var root = new TemporaryPersistenceDirectory();
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
            await store.UpsertAsync(new Serial(1), [1]);
            await store.AbortAsync();
        }
        var path = System.IO.Path.Combine(root.Path, "items.journal.bin");
        var bytes = File.ReadAllBytes(path);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(108), sequence);
        PersistenceFixture.Rechecksum(bytes, 100, 28);
        File.WriteAllBytes(path, bytes);
        await using var failed = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await Assert.ThrowsAsync<InvalidDataException>(() => failed.InitializeAsync());
    }

    [Fact]
    public async Task UpsertAsync_SequenceAtMaximum_RejectsWithoutFaultingReads()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
        }

        foreach (var file in new[] { "items.snapshot.bin", "items.journal.bin" })
        {
            var path = System.IO.Path.Combine(root.Path, file);
            var bytes = File.ReadAllBytes(path);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16), ulong.MaxValue);
            PersistenceFixture.Rechecksum(bytes, 0, 96);
            File.WriteAllBytes(path, bytes);
        }
        await using var reopened = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await reopened.InitializeAsync();
        await Assert.ThrowsAsync<OverflowException>(() => reopened.UpsertAsync(new Serial(1), [1]));
        Assert.Empty(reopened.Capture());
    }

    [Fact]
    public async Task UpsertAsync_ThresholdReached_CheckpointsBeforeNextCommit()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions { JournalCheckpointThresholdBytes = 133 });
        await store.InitializeAsync();
        await store.UpsertAsync(new Serial(1), [1]);
        await store.UpsertAsync(new Serial(2), [2]);
        var snapshot = File.ReadAllBytes(System.IO.Path.Combine(root.Path, "items.snapshot.bin"));
        Assert.Equal(1UL, BinaryPrimitives.ReadUInt64LittleEndian(snapshot.AsSpan(16)));
        Assert.Equal(133, new FileInfo(System.IO.Path.Combine(root.Path, "items.journal.bin")).Length);
    }

    [Fact]
    public async Task CheckpointAsync_PublicationFails_FaultsAndDisposalReleasesLockWithoutCheckpoint()
    {
        using var root = new TemporaryPersistenceDirectory();
        var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await store.InitializeAsync();
        await store.UpsertAsync(new Serial(1), [1]);
        var snapshot = System.IO.Path.Combine(root.Path, "items.snapshot.bin");
        var bytes = File.ReadAllBytes(snapshot);
        File.Delete(snapshot);
        Directory.CreateDirectory(snapshot);
        await Assert.ThrowsAnyAsync<IOException>(() => store.CheckpointAsync());
        Assert.Throws<InvalidOperationException>(() => store.Capture());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpsertAsync(new Serial(2), [2]));
        await store.DisposeAsync();
        Directory.Delete(snapshot);
        File.WriteAllBytes(snapshot, bytes);
        await using var reopened = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await reopened.InitializeAsync();
        Assert.Equal(new byte[] { 1 }, reopened.Get(new Serial(1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../items")]
    [InlineData("Items")]
    [InlineData("itëms")]
    public void Constructor_InvalidName_Rejects(string name)
    {
        Assert.Throws<ArgumentException>(() => new BinaryCollectionStore("unused", name, new PersistenceOptions()));
    }

    [Fact]
    public void Constructor_InvalidOptions_Rejects()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BinaryCollectionStore("unused", "items", new PersistenceOptions { MaxPayloadBytes = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BinaryCollectionStore("unused", "items", new PersistenceOptions { JournalCheckpointThresholdBytes = 0 }));
    }

    [Fact]
    public async Task InitializeAsync_JournalWithoutShutdownCheckpoint_RecoversCommittedPayload()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
            await store.UpsertAsync(new Serial(1), [4, 2]);
            Assert.Equal(new byte[] { 4, 2 }, store.Get(new Serial(1)));
            await store.AbortAsync();
        }
        await using var reopened = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await reopened.InitializeAsync();
        Assert.Equal(new byte[] { 4, 2 }, reopened.Get(new Serial(1)));
    }

    [Fact]
    public async Task Mutations_ReplaceDeleteAndCapture_PreserveCommittedBuffers()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await store.InitializeAsync();
        byte[] input = [1];
        await store.UpsertAsync(new Serial(1), input);
        input[0] = 9;
        var captured = await store.CaptureAsync();
        await store.UpsertAsync(new Serial(1), [2]);
        Assert.Equal(new byte[] { 1 }, captured.Single());
        Assert.Equal(new byte[] { 2 }, store.Capture().Single());
        Assert.Equal(new Serial(1), store.CaptureEntries().Single().Key);
        Assert.True(await store.DeleteAsync(new Serial(1)));
        var length = new FileInfo(System.IO.Path.Combine(root.Path, "items.journal.bin")).Length;
        Assert.False(await store.DeleteAsync(new Serial(1)));
        Assert.Equal(length, new FileInfo(System.IO.Path.Combine(root.Path, "items.journal.bin")).Length);
        Assert.Null(store.Get(new Serial(1)));
    }

    [Theory]
    [InlineData("items.snapshot.bin")]
    [InlineData("items.journal.bin")]
    public async Task InitializeAsync_OneCommittedFileMissingAtZero_FailsAndReleasesLock(string missing)
    {
        using var root = new TemporaryPersistenceDirectory();
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
        }

        File.Delete(System.IO.Path.Combine(root.Path, missing));
        await using var failed = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await Assert.ThrowsAsync<InvalidDataException>(() => failed.InitializeAsync());
        using var acquired = new FileStream(System.IO.Path.Combine(root.Path, "items.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public async Task InitializeAsync_ExistingWriter_RejectsUntilReleased()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var first = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await first.InitializeAsync();
        await using var second = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await Assert.ThrowsAsync<IOException>(() => second.InitializeAsync());
        await first.AbortAsync();
        await using var third = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await third.InitializeAsync();
    }

    [Fact]
    public async Task UpsertAsync_InvalidInputOrCancellation_LeavesJournalUnchanged()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions { MaxPayloadBytes = 2 });
        await store.InitializeAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.UpsertAsync(Serial.Zero, [1]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.UpsertAsync(new Serial(1), []));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.UpsertAsync(new Serial(1), [1, 2, 3]));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.UpsertAsync(new Serial(1), [1], new CancellationToken(true)));
        Assert.Empty(store.Capture());
        Assert.Equal(100, new FileInfo(System.IO.Path.Combine(root.Path, "items.journal.bin")).Length);
    }

    [Fact]
    public async Task CheckpointAsync_OldJournalWithNewSnapshot_ReplaysLaterChanges()
    {
        using var root = new TemporaryPersistenceDirectory();
        var journal = System.IO.Path.Combine(root.Path, "items.journal.bin");
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
            await store.UpsertAsync(new Serial(1), [1]);
            var oldJournal = File.ReadAllBytes(journal);
            await store.CheckpointAsync();
            await store.AbortAsync();
            File.WriteAllBytes(journal, oldJournal);
        }
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
            await store.UpsertAsync(new Serial(2), [2]);
            await store.CheckpointAsync();
        }
        await using var reopened = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await reopened.InitializeAsync();
        Assert.Equal(2, reopened.Capture().Length);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(33)]
    public async Task InitializeAsync_IncompleteFinalRecord_TruncatesBeforeNextAppend(int tailLength)
    {
        using var root = new TemporaryPersistenceDirectory();
        var journal = System.IO.Path.Combine(root.Path, "items.journal.bin");
        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
            await store.UpsertAsync(new Serial(1), [1]);
            await store.UpsertAsync(new Serial(2), [2, 3]);
            await store.AbortAsync();
        }
        using (var stream = File.OpenWrite(journal))
        {
            stream.SetLength(133 + tailLength);
        }

        await using (var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions()))
        {
            await store.InitializeAsync();
            Assert.Single(store.Capture());
            Assert.Equal(133, new FileInfo(journal).Length);
            await store.UpsertAsync(new Serial(3), [3]);
            await store.AbortAsync();
        }
        await using var reopened = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        await reopened.InitializeAsync();
        Assert.Equal(2, reopened.Capture().Length);
    }

    [Fact]
    public async Task DisposeAsync_ClosedStore_RejectsAllOperations()
    {
        using var root = new TemporaryPersistenceDirectory();
        var store = new BinaryCollectionStore(root.Path, "items", new PersistenceOptions());
        Assert.Throws<InvalidOperationException>(() => store.Capture());
        await store.InitializeAsync();
        await store.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => store.Get(new Serial(1)));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => store.UpsertAsync(new Serial(1), [1]));
        await store.DisposeAsync();
    }
}
