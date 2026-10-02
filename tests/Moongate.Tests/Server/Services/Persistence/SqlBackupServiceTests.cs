using Microsoft.Extensions.Time.Testing;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Core.Data.Persistence;
using Moongate.Server.Services.Persistence;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Commands;

namespace Moongate.Tests.Server.Services.Persistence;

public sealed class SqlBackupServiceTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly TemporaryDirectory _root = new();

    private readonly FakeTimeProvider _time = new(new(2026, 10, 2, 11, 30, 0, TimeSpan.Zero));

    private string Backups => Path.Combine(_root.Path, "backups");

    [Fact]
    public async Task BackupAsync_Standalone_WritesAuthThenWorldWithTheUtcDate()
    {
        var exporter = new RecordingDataExporter(PersistenceDatabaseTarget.Realm, PersistenceDatabaseTarget.Accounts);
        var service = Create(exporter);

        var result = await service.BackupAsync();

        Assert.Equal([PersistenceDatabaseTarget.Accounts, PersistenceDatabaseTarget.Realm], exporter.Calls);
        Assert.Equal(["auth_20261002_113000.sql", "world_20261002_113000.sql"], FileNames());
        Assert.Equal("-- Realm\n", File.ReadAllText(Path.Combine(Backups, "world_20261002_113000.sql")));
        Assert.Equal(["auth", "world"], result.Files.Select(file => file.Database));
        Assert.All(result.Files, file => Assert.Equal(new FileInfo(file.Path).Length, file.Size));
        Assert.Empty(result.Failures);
        Assert.False(result.AlreadyRunning);
    }

    [Theory]
    [InlineData(PersistenceDatabaseTarget.Accounts, "auth_20261002_113000.sql")]
    [InlineData(PersistenceDatabaseTarget.Realm, "world_20261002_113000.sql")]
    public async Task BackupAsync_OneRole_WritesOnlyItsDatabase(PersistenceDatabaseTarget target, string expected)
    {
        var service = Create(new RecordingDataExporter(target));

        await service.BackupAsync();

        Assert.Equal([expected], FileNames());
    }

    [Fact]
    public async Task BackupAsync_WithAWorldSaveService_SavesBeforeItExports()
    {
        var exporter = new RecordingDataExporter(PersistenceDatabaseTarget.Realm);
        var saves = new ControlledWorldSaveService();
        var service = Create(exporter, worldSave: saves);

        var backup = service.BackupAsync();

        Assert.Equal(1, saves.Calls);
        Assert.Empty(exporter.Calls);
        saves.Completion.SetResult();
        await backup.WaitAsync(Timeout);
        Assert.Single(exporter.Calls);
    }

    [Fact]
    public async Task BackupAsync_WhenTheWorldSaveFails_WritesNothingAndReportsIt()
    {
        var exporter = new RecordingDataExporter(PersistenceDatabaseTarget.Accounts, PersistenceDatabaseTarget.Realm);
        var saves = new ControlledWorldSaveService();
        saves.Completion.SetException(new InvalidOperationException("disk full"));
        var service = Create(exporter, worldSave: saves);

        var result = await service.BackupAsync();

        Assert.Empty(exporter.Calls);
        Assert.Empty(result.Files);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(("world", "disk full"), (failure.Database, failure.Reason));
        Assert.False(Directory.Exists(Backups) && Directory.EnumerateFileSystemEntries(Backups).Any());
    }

    [Fact]
    public async Task BackupAsync_WhenOneDatabaseFails_StillWritesTheOtherAndLeavesNoTemporaryFile()
    {
        var exporter = new RecordingDataExporter(PersistenceDatabaseTarget.Accounts, PersistenceDatabaseTarget.Realm);
        exporter.Failing.Add(PersistenceDatabaseTarget.Accounts);
        var service = Create(exporter);

        var result = await service.BackupAsync();

        Assert.Equal(["world_20261002_113000.sql"], FileNames());
        Assert.Equal("auth", Assert.Single(result.Failures).Database);
        Assert.Equal("world", Assert.Single(result.Files).Database);
    }

    [Fact]
    public async Task BackupAsync_AFailedDatabase_DoesNotRotateItsOlderFiles()
    {
        Seed("world_20260101_000000.sql", "world_20260102_000000.sql");
        var exporter = new RecordingDataExporter(PersistenceDatabaseTarget.Realm);
        exporter.Failing.Add(PersistenceDatabaseTarget.Realm);
        var service = Create(exporter, keep: 1);

        await service.BackupAsync();

        Assert.Equal(["world_20260101_000000.sql", "world_20260102_000000.sql"], FileNames());
    }

    [Fact]
    public async Task BackupAsync_Rotation_KeepsTheNewestCopiesOfEachDatabaseAndNothingElseIsTouched()
    {
        Seed(
            "world_20260101_000000.sql",
            "world_20260102_000000.sql",
            "world_20260103_000000.sql",
            "auth_20260101_000000.sql",
            "world_manual.sql",
            "world_20260101_000000.sql.bak",
            "auth_20260101_000000.sqlx",
            "notes.txt"
        );
        var service = Create(new RecordingDataExporter(PersistenceDatabaseTarget.Realm), keep: 2);

        await service.BackupAsync();

        Assert.Equal(
            [
                "auth_20260101_000000.sql",
                "auth_20260101_000000.sqlx",
                "notes.txt",
                "world_20260101_000000.sql.bak",
                "world_20260103_000000.sql",
                "world_20261002_113000.sql",
                "world_manual.sql"
            ],
            FileNames()
        );
    }

    [Fact]
    public async Task BackupAsync_WhileAnotherBackupRuns_IsRefused()
    {
        var exporter = new RecordingDataExporter(PersistenceDatabaseTarget.Realm)
        {
            Gate = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var service = Create(exporter);

        var first = service.BackupAsync();
        var second = await service.BackupAsync();

        Assert.True(second.AlreadyRunning);
        Assert.Empty(second.Files);
        exporter.Gate.SetResult();
        Assert.False((await first.WaitAsync(Timeout)).AlreadyRunning);
        Assert.False((await service.BackupAsync()).AlreadyRunning);
    }

    [Fact]
    public async Task BackupAsync_WhenTheDirectoryCannotBeCreated_ReportsEveryDatabaseAndDoesNotThrow()
    {
        File.WriteAllText(Backups, "a file where the directory should be");
        var service = Create(
            new RecordingDataExporter(PersistenceDatabaseTarget.Accounts, PersistenceDatabaseTarget.Realm)
        );

        var result = await service.BackupAsync();

        Assert.Empty(result.Files);
        Assert.Equal(["auth", "world"], result.Failures.Select(failure => failure.Database));
    }

    [Fact]
    public async Task BackupAsync_ACanceledRequest_RemovesItsTemporaryFile()
    {
        var exporter = new RecordingDataExporter(PersistenceDatabaseTarget.Realm)
        {
            Gate = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var service = Create(exporter);
        using var cancellation = new CancellationTokenSource();

        var backup = service.BackupAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => backup.WaitAsync(Timeout));
        Assert.Empty(FileNames());
    }

    [Fact]
    public async Task StartAsync_RemovesLeftoverTemporaryFilesAndNothingElse()
    {
        Seed("world_20260101_000000.sql.tmp", "auth_20260101_000000.sql.tmp", "world_20260101_000000.sql", "other.tmp");
        var service = Create(new RecordingDataExporter(PersistenceDatabaseTarget.Realm));

        await service.StartAsync();
        await service.StopAsync();

        Assert.Equal(["other.tmp", "world_20260101_000000.sql"], FileNames());
    }

    [Fact]
    public async Task StartAsync_WhenEnabled_BacksUpAfterEachIntervalAndNotAtStartup()
    {
        var exporter = new RecordingDataExporter(PersistenceDatabaseTarget.Realm);
        var service = Create(exporter, enabled: true, interval: TimeSpan.FromMinutes(10));

        await service.StartAsync();

        try
        {
            Assert.Empty(exporter.Calls);

            _time.Advance(TimeSpan.FromMinutes(10));
            await WaitForAsync(() => FinishedFiles() == 1);

            _time.Advance(TimeSpan.FromMinutes(10));
            await WaitForAsync(() => FinishedFiles() == 2);
            Assert.Equal(["world_20261002_114000.sql", "world_20261002_115000.sql"], FileNames());
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task StartAsync_WhenDisabled_NeverBacksUpOnItsOwn()
    {
        var exporter = new RecordingDataExporter(PersistenceDatabaseTarget.Realm);
        var service = Create(exporter, enabled: false, interval: TimeSpan.FromMinutes(1));

        await service.StartAsync();
        _time.Advance(TimeSpan.FromHours(1));
        await Task.Delay(100);
        await service.StopAsync();

        Assert.Empty(exporter.Calls);
    }

    [Fact]
    public async Task StartAsync_AScheduledBackupThatFails_DoesNotStopTheSchedule()
    {
        var exporter = new RecordingDataExporter(PersistenceDatabaseTarget.Realm);
        exporter.Failing.Add(PersistenceDatabaseTarget.Realm);
        var service = Create(exporter, enabled: true, interval: TimeSpan.FromMinutes(10));

        await service.StartAsync();

        try
        {
            _time.Advance(TimeSpan.FromMinutes(10));
            await WaitForAsync(() => exporter.Calls.Count == 1);
            exporter.Failing.Clear();

            _time.Advance(TimeSpan.FromMinutes(10));
            await WaitForAsync(() => FinishedFiles() == 1);
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_CalledTwiceOrWithoutStart_DoesNothing()
    {
        var service = Create(new RecordingDataExporter(PersistenceDatabaseTarget.Realm));

        await service.StopAsync();
        await service.StopAsync();
    }

    private SqlBackupService Create(
        RecordingDataExporter exporter,
        bool enabled = false,
        TimeSpan? interval = null,
        int keep = 5,
        ControlledWorldSaveService? worldSave = null
    )
    {
        var options = new SqlBackupOptions
        {
            Enabled = enabled,
            Interval = interval ?? TimeSpan.FromMinutes(1440),
            Directory = Backups,
            Keep = keep
        };

        return new(exporter, options, _time, worldSave);
    }

    private string[] FileNames()
    {
        return Directory.Exists(Backups)
            ? Directory.EnumerateFiles(Backups).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!
            : [];
    }

    // A file still being written has the temporary suffix: only whole files count.
    private int FinishedFiles()
    {
        return FileNames().Count(name => name.EndsWith(".sql", StringComparison.Ordinal));
    }

    private void Seed(params string[] names)
    {
        Directory.CreateDirectory(Backups);

        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(Backups, name), name);
        }
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was not met in time.");
            await Task.Delay(10);
        }
    }

    public void Dispose()
    {
        _root.Dispose();
    }
}
