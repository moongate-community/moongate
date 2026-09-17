using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Moongate.Core.Primitives;
using Moongate.Persistence.Data;
using Moongate.Persistence.Services;
using Moongate.Tests.Support.Persistence;

namespace Moongate.Tests.Integration.Persistence;

public sealed class MoongatePersistenceBackupTests
{
    [Fact]
    public async Task SaveAndRestore_PreservesLiveAndExplicitCollectionsAndDeletions()
    {
        using var root = new TemporaryPersistenceDirectory();
        var source = Path.Combine(root.Path, "source");
        var backup = Path.Combine(root.Path, "backup");
        var destination = Path.Combine(root.Path, "restored");
        var live = new TestEntity { Id = new Serial(1), Name = "initial" };
        await using var owner = new MoongatePersistenceService(source);
        var items = owner.Register<TestEntity>("items", () => [live]);
        var others = owner.Register<OtherTestEntity>("others");
        await owner.InitializeAsync();
        await owner.SaveAllAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(2), Name = "deleted" });
        await items.DeleteAsync(new Serial(2));
        await others.UpsertAsync(new OtherTestEntity { Id = new Serial(3), Value = 42 });
        live.Name = "saved";

        await owner.SaveAllWithBackupAsync(CaptureAsync, backup);
        live.Name = "later";
        await items.UpsertAsync(new TestEntity { Id = new Serial(4), Name = "later" });
        await MoongatePersistenceBackup.RestoreAsync(backup, destination);

        await using var restored = new MoongatePersistenceService(destination);
        var restoredItems = restored.Register<TestEntity>("items");
        var restoredOthers = restored.Register<OtherTestEntity>("others");
        await restored.InitializeAsync();
        Assert.Equal("saved", Assert.Single(restoredItems.GetAll()).Name);
        Assert.Null(restoredItems.GetById(new Serial(2)));
        Assert.Null(restoredItems.GetById(new Serial(4)));
        Assert.Equal(42, Assert.Single(restoredOthers.GetAll()).Value);
    }

    [Fact]
    public async Task SaveAndRestore_NoCollections_PublishesAndRestoresEmptyGeneration()
    {
        using var root = new TemporaryPersistenceDirectory();
        await using var owner = new MoongatePersistenceService(Path.Combine(root.Path, "source"));
        await owner.InitializeAsync();
        var backup = Path.Combine(root.Path, "backup");
        var destination = Path.Combine(root.Path, "restored");

        await owner.SaveAllWithBackupAsync(CaptureAsync, backup);
        Assert.Equal("manifest.json", Path.GetFileName(Assert.Single(Directory.GetFiles(backup))));
        await MoongatePersistenceBackup.RestoreAsync(backup, destination);
        Assert.Empty(Directory.GetFileSystemEntries(destination));
    }

    [Fact]
    public async Task SaveAllWithBackupAsync_CopiesOnlyRegisteredBinaryPairs()
    {
        using var root = new TemporaryPersistenceDirectory();
        var source = Path.Combine(root.Path, "source");
        await using var owner = new MoongatePersistenceService(source);
        owner.Register<TestEntity>("items");
        await owner.InitializeAsync();
        await File.WriteAllTextAsync(Path.Combine(source, "items.snapshot.bin.foreign.tmp"), "keep");
        await File.WriteAllTextAsync(Path.Combine(source, "unregistered.snapshot.bin"), "keep");
        var backup = Path.Combine(root.Path, "backup");

        await owner.SaveAllWithBackupAsync(CaptureAsync, backup);

        Assert.Equal(
            new[] { "items.journal.bin", "items.snapshot.bin", "manifest.json" },
            Directory.GetFiles(backup).Select(Path.GetFileName).Order()
        );
    }

    [Theory,
     InlineData("bytes"), InlineData("truncated"), InlineData("hash"), InlineData("missing-file"),
     InlineData("version"), InlineData("missing-pair"), InlineData("duplicate-collection"),
     InlineData("duplicate-file"), InlineData("unsafe-file"), InlineData("unsafe-collection"),
     InlineData("dropped-collection"), InlineData("malformed-json"), InlineData("null-collections"),
     InlineData("wrong-identity-with-valid-hash")]
    public async Task RestoreAsync_DamagedGeneration_RejectsWithoutExposingDestination(string damage)
    {
        using var root = new TemporaryPersistenceDirectory();
        var backup = await CreateBackupAsync(root.Path);
        var manifestPath = Path.Combine(backup, "manifest.json");
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!;
        var collections = manifest["collections"]!.AsArray();
        var collection = collections[0]!;
        var snapshot = collection["snapshot"]!;
        var snapshotPath = Path.Combine(backup, "items.snapshot.bin");
        switch (damage)
        {
            case "bytes":
                var bytes = await File.ReadAllBytesAsync(snapshotPath);
                bytes[^1] ^= 0x10;
                await File.WriteAllBytesAsync(snapshotPath, bytes);
                break;
            case "truncated":
                await File.WriteAllBytesAsync(snapshotPath, [0]);
                break;
            case "hash":
                snapshot["sha256"] = new string('0', 64);
                break;
            case "missing-file":
                File.Delete(Path.Combine(backup, "items.journal.bin"));
                break;
            case "version":
                manifest["formatVersion"] = 2;
                break;
            case "missing-pair":
                collection.AsObject().Remove("journal");
                break;
            case "duplicate-collection":
                collections.Add(collection.DeepClone());
                break;
            case "duplicate-file":
                collection["journal"] = snapshot.DeepClone();
                break;
            case "unsafe-file":
                snapshot["fileName"] = "../items.snapshot.bin";
                break;
            case "unsafe-collection":
                collection["name"] = "../items";
                break;
            case "dropped-collection":
                collections.Clear();
                break;
            case "null-collections":
                manifest["collections"] = null;
                break;
            case "wrong-identity-with-valid-hash":
                var journal = await File.ReadAllBytesAsync(Path.Combine(backup, "items.journal.bin"));
                await File.WriteAllBytesAsync(snapshotPath, journal);
                snapshot["length"] = journal.Length;
                snapshot["sha256"] = Convert.ToHexString(SHA256.HashData(journal));
                break;
        }
        await File.WriteAllTextAsync(manifestPath, damage == "malformed-json" ? "{" : manifest.ToJsonString());
        var destination = Path.Combine(root.Path, "restored");

        await Assert.ThrowsAsync<InvalidDataException>(() => MoongatePersistenceBackup.RestoreAsync(backup, destination));

        Assert.False(Directory.Exists(destination));
        Assert.Equal(new[] { "backup", "source" }, Directory.GetDirectories(root.Path).Select(Path.GetFileName).Order());
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task RestoreAsync_ExistingDestination_PreservesContents(bool isFile)
    {
        using var root = new TemporaryPersistenceDirectory();
        var backup = await CreateBackupAsync(root.Path);
        var destination = Path.Combine(root.Path, "restored");
        if (!isFile)
        {
            Directory.CreateDirectory(destination);
        }
        var sentinel = isFile ? destination : Path.Combine(destination, "sentinel");
        await File.WriteAllTextAsync(sentinel, "untouched");

        await Assert.ThrowsAsync<IOException>(() => MoongatePersistenceBackup.RestoreAsync(backup, destination));

        Assert.Equal("untouched", await File.ReadAllTextAsync(sentinel));
    }

    [Fact]
    public async Task RestoreAsync_OverlappingOrCanceledDestination_DoesNotCreateFiles()
    {
        using var root = new TemporaryPersistenceDirectory();
        var backup = await CreateBackupAsync(root.Path);
        var inside = Path.Combine(backup, "restored");
        var destination = Path.Combine(root.Path, "restored");

        await Assert.ThrowsAsync<ArgumentException>(() => MoongatePersistenceBackup.RestoreAsync(backup, inside));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            MoongatePersistenceBackup.RestoreAsync(backup, destination, new CancellationToken(true)));

        Assert.False(Directory.Exists(inside));
        Assert.False(Directory.Exists(destination));
    }

    [Theory, InlineData("existing"), InlineData("file"), InlineData("inside"), InlineData("same"),
     InlineData("parent"), InlineData("blank")]
    public async Task SaveAllWithBackupAsync_InvalidTarget_DoesNotCaptureOrCheckpoint(string targetKind)
    {
        using var root = new TemporaryPersistenceDirectory();
        var source = Path.Combine(root.Path, "source");
        await using var owner = new MoongatePersistenceService(source);
        var items = owner.Register<TestEntity>("items");
        await owner.InitializeAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "journal" });
        var journalPath = Path.Combine(source, "items.journal.bin");
        var journal = await File.ReadAllBytesAsync(journalPath);
        var target = targetKind switch
        {
            "inside" => Path.Combine(source, "backup"),
            "same" => source,
            "parent" => root.Path,
            "blank" => " ",
            _ => Path.Combine(root.Path, "existing")
        };
        if (targetKind == "existing")
        {
            Directory.CreateDirectory(target);
        }
        else if (targetKind == "file")
        {
            await File.WriteAllTextAsync(target, "untouched");
        }
        var captureCalled = false;

        await Assert.ThrowsAnyAsync<Exception>(() => owner.SaveAllWithBackupAsync((capture, _) =>
        {
            captureCalled = true;
            capture();
            return Task.CompletedTask;
        }, target));

        Assert.False(captureCalled);
        Assert.Equal(journal, await File.ReadAllBytesAsync(journalPath));
        if (targetKind == "file")
        {
            Assert.Equal("untouched", await File.ReadAllTextAsync(target));
        }
    }

    [Fact]
    public async Task SaveAllWithBackupAsync_CanceledDuringCapture_CleansOnlyOwnedStaging()
    {
        using var root = new TemporaryPersistenceDirectory();
        var source = Path.Combine(root.Path, "source");
        var backup = Path.Combine(root.Path, "backup");
        var foreign = Path.Combine(root.Path, ".backup.foreign.tmp");
        Directory.CreateDirectory(foreign);
        await File.WriteAllTextAsync(Path.Combine(foreign, "sentinel"), "untouched");
        using var cancellation = new CancellationTokenSource();
        await using var owner = new MoongatePersistenceService(source);
        owner.Register<TestEntity>("items");
        await owner.InitializeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.SaveAllWithBackupAsync((capture, _) =>
        {
            capture();
            cancellation.Cancel();
            return Task.CompletedTask;
        }, backup, cancellation.Token));

        Assert.False(Directory.Exists(backup));
        Assert.Equal("untouched", await File.ReadAllTextAsync(Path.Combine(foreign, "sentinel")));
        Assert.Equal(new[] { ".backup.foreign.tmp", "source" },
            Directory.GetDirectories(root.Path).Select(Path.GetFileName).Order());
        await owner.SaveAllWithBackupAsync(CaptureAsync, backup);
    }

    [Fact]
    public async Task SaveAllWithBackupAsync_DestinationAppearsDuringCapture_PreservesIt()
    {
        using var root = new TemporaryPersistenceDirectory();
        var backup = Path.Combine(root.Path, "backup");
        await using var owner = new MoongatePersistenceService(Path.Combine(root.Path, "source"));
        owner.Register<TestEntity>("items");
        await owner.InitializeAsync();

        await Assert.ThrowsAsync<IOException>(() => owner.SaveAllWithBackupAsync((capture, _) =>
        {
            capture();
            Directory.CreateDirectory(backup);
            File.WriteAllText(Path.Combine(backup, "sentinel"), "untouched");
            return Task.CompletedTask;
        }, backup));

        Assert.Equal("untouched", await File.ReadAllTextAsync(Path.Combine(backup, "sentinel")));
        Assert.Single(Directory.GetFiles(backup));
        Assert.Equal(2, Directory.GetDirectories(root.Path).Length);
    }

    [Fact]
    public async Task SaveAllWithBackupAsync_FailedCheckpoint_DoesNotPublishGeneration()
    {
        using var root = new TemporaryPersistenceDirectory();
        var source = Path.Combine(root.Path, "source");
        var backup = Path.Combine(root.Path, "backup");
        await using var owner = new MoongatePersistenceService(source);
        owner.Register<TestEntity>("items");
        await owner.InitializeAsync();
        var snapshotPath = Path.Combine(source, "items.snapshot.bin");
        File.Delete(snapshotPath);
        Directory.CreateDirectory(snapshotPath);

        await Assert.ThrowsAnyAsync<IOException>(() => owner.SaveAllWithBackupAsync(CaptureAsync, backup));

        Assert.False(Directory.Exists(backup));
        Assert.Equal(source, Assert.Single(Directory.GetDirectories(root.Path)));
    }

    [Fact]
    public async Task SaveAllWithBackupAsync_ConcurrentMutations_WaitUntilGenerationIsPublished()
    {
        using var root = new TemporaryPersistenceDirectory();
        var backup = Path.Combine(root.Path, "backup");
        var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var owner = new MoongatePersistenceService(Path.Combine(root.Path, "source"));
        var items = owner.Register<TestEntity>("items", () => [new TestEntity { Id = new Serial(1), Name = "saved" }]);
        await owner.InitializeAsync();
        var save = owner.SaveAllWithBackupAsync(async (capture, token) =>
        {
            capture();
            captured.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        }, backup);
        await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var delete = items.DeleteAsync(new Serial(1));
        var upsert = items.UpsertAsync(new TestEntity { Id = new Serial(2), Name = "later" });
        try
        {
            Assert.False(delete.IsCompleted);
            Assert.False(upsert.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }
        await Task.WhenAll(save, delete, upsert).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(items.GetById(new Serial(1)));
        var restoredPath = Path.Combine(root.Path, "restored");
        await MoongatePersistenceBackup.RestoreAsync(backup, restoredPath);
        await using var restored = new MoongatePersistenceService(restoredPath);
        var restoredItems = restored.Register<TestEntity>("items");
        await restored.InitializeAsync();
        Assert.Equal("saved", Assert.Single(restoredItems.GetAll()).Name);
    }

    [Fact]
    public async Task SaveAndRestore_CustomPayloadLimit_PreservesPayloadLargerThanDefaultLimit()
    {
        using var root = new TemporaryPersistenceDirectory();
        var options = new PersistenceOptions { MaxPayloadBytes = 20 * 1024 * 1024 };
        var name = new string('x', 16 * 1024 * 1024 + 1024);
        var backup = Path.Combine(root.Path, "backup");
        var destination = Path.Combine(root.Path, "restored");
        await using var owner = new MoongatePersistenceService(Path.Combine(root.Path, "source"), options);
        var items = owner.Register<TestEntity>("items");
        await owner.InitializeAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(1), Name = name });

        await owner.SaveAllWithBackupAsync(CaptureAsync, backup);
        await MoongatePersistenceBackup.RestoreAsync(backup, destination);

        await using var restored = new MoongatePersistenceService(destination, options);
        var restoredItems = restored.Register<TestEntity>("items");
        await restored.InitializeAsync();
        Assert.Equal(name, restoredItems.GetById(new Serial(1))!.Name);
    }

    [Fact]
    public async Task SaveAndRestore_OverlappingDirectoryAliases_RejectBeforeSideEffects()
    {
        // Creating symbolic links on Windows requires privileges not held by every test runner.
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        using var root = new TemporaryPersistenceDirectory();
        var source = Path.Combine(root.Path, "source");
        var alias = Path.Combine(root.Path, "alias");
        await using var owner = new MoongatePersistenceService(source);
        owner.Register<TestEntity>("items");
        await owner.InitializeAsync();
        Directory.CreateSymbolicLink(alias, source);
        var captureCalled = false;

        await Assert.ThrowsAsync<ArgumentException>(() => owner.SaveAllWithBackupAsync((_, _) =>
        {
            captureCalled = true;
            return Task.CompletedTask;
        }, Path.Combine(alias, "backup")));

        Assert.False(captureCalled);
        Assert.False(Directory.Exists(Path.Combine(source, "backup")));
        var backup = Path.Combine(root.Path, "backup");
        await owner.SaveAllWithBackupAsync(CaptureAsync, backup);
        var backupAlias = Path.Combine(root.Path, "backup-alias");
        Directory.CreateSymbolicLink(backupAlias, backup);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            MoongatePersistenceBackup.RestoreAsync(backup, Path.Combine(backupAlias, "restored")));
        Assert.False(Directory.Exists(Path.Combine(backup, "restored")));
    }

    private static async Task<string> CreateBackupAsync(string root)
    {
        await using var owner = new MoongatePersistenceService(Path.Combine(root, "source"));
        var items = owner.Register<TestEntity>("items");
        await owner.InitializeAsync();
        await items.UpsertAsync(new TestEntity { Id = new Serial(1), Name = "saved" });
        var backup = Path.Combine(root, "backup");
        await owner.SaveAllWithBackupAsync(CaptureAsync, backup);

        return backup;
    }

    private static Task CaptureAsync(Action capture, CancellationToken cancellationToken)
    {
        capture();
        return Task.CompletedTask;
    }
}
