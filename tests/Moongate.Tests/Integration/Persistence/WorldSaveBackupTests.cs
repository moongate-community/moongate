using Moongate.Core.Primitives;
using Moongate.Persistence.Services;
using Moongate.Tests.Support.Persistence;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Persistence;

public sealed class WorldSaveBackupTests
{
    [Fact]
    public async Task SaveAsync_DefaultRetention_KeepsFiveNewestCompletedBackupsAndPreservesForeignAndIncompleteDirectories()
    {
        await using var fixture = new WorldSaveFixture(backups: true);
        await fixture.StartAsync();
        var foreign = Path.Combine(fixture.BackupDirectory, "operator-backup");
        var incomplete = Path.Combine(fixture.BackupDirectory, "world-save-20200101T0000000000000Z-" + new string('a', 32));
        Directory.CreateDirectory(foreign);
        Directory.CreateDirectory(incomplete);
        File.WriteAllText(Path.Combine(foreign, MoongatePersistenceBackup.ManifestFileName), "foreign data");
        var published = new List<string>();
        for (var index = 0; index < 7; index++)
        {
            fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await fixture.OnLoopAsync(() => fixture.Entities[0].Name = $"generation {index}");
            await fixture.Saves.SaveAsync();
            published.Add(Directory.GetDirectories(fixture.BackupDirectory).Except([foreign, incomplete]).Except(published).Single());
        }
        var surviving = Directory.GetDirectories(fixture.BackupDirectory).Except([foreign, incomplete]).ToArray();
        Assert.Equal(published.Skip(2).Order(), surviving.Order());
        Assert.True(Directory.Exists(foreign));
        Assert.True(Directory.Exists(incomplete));
        using var restoreRoot = new TemporaryPersistenceDirectory();
        var destination = Path.Combine(restoreRoot.Path, "restored");
        await MoongatePersistenceBackup.RestoreAsync(published[^1], destination);
        await using var restored = new MoongatePersistenceService(destination);
        var items = restored.Register<TestEntity>("items");
        await restored.InitializeAsync();
        Assert.Equal("generation 6", items.GetById(new Serial(7))?.Name);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task SaveAsync_UtcTiesOrMovesBackward_AlwaysRetainsJustPublishedGeneration(bool moveBackward)
    {
        await using var fixture = new WorldSaveFixture(backups: true, retention: 1);
        await fixture.StartAsync();
        await fixture.Saves.SaveAsync();
        var previous = Directory.GetDirectories(fixture.BackupDirectory).Single();
        if (moveBackward)
        {
            fixture.Clock.UtcNow -= TimeSpan.FromDays(1);
        }
        await fixture.OnLoopAsync(() => fixture.Entities[0].Name = "newest");
        await fixture.Saves.SaveAsync();
        var newest = Directory.GetDirectories(fixture.BackupDirectory).Single();
        Assert.NotEqual(previous, newest);
        using var root = new TemporaryPersistenceDirectory();
        var destination = Path.Combine(root.Path, "restored");
        await MoongatePersistenceBackup.RestoreAsync(newest, destination);
        await using var restored = new MoongatePersistenceService(destination);
        var items = restored.Register<TestEntity>("items");
        await restored.InitializeAsync();
        Assert.Equal("newest", items.GetById(new Serial(7))?.Name);
    }

    [Fact]
    public async Task SaveAsync_RepeatedClockRollback_RetainsMostRecentGenerationsInPublicationOrder()
    {
        await using var fixture = new WorldSaveFixture(backups: true, retention: 2);
        await fixture.StartAsync();
        var generations = new List<string>();
        for (var index = 0; index < 3; index++)
        {
            fixture.Clock.UtcNow -= TimeSpan.FromDays(1);
            await fixture.Saves.SaveAsync();
            generations.Add(Directory.GetDirectories(fixture.BackupDirectory).Except(generations).Single());
        }
        Assert.Equal(generations.Skip(1).Order(), Directory.GetDirectories(fixture.BackupDirectory).Order());
    }

    [Fact]
    public async Task SaveAsync_FailedCapture_DoesNotPruneLastSuccessfulBackupOrPublishPartialGeneration()
    {
        await using var fixture = new WorldSaveFixture(backups: true, retention: 1);
        await fixture.StartAsync();
        await fixture.Saves.SaveAsync();
        var completed = Directory.GetDirectories(fixture.BackupDirectory).Single();
        var failure = new ApplicationException("capture failed");
        fixture.CaptureFailure = failure;
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.Saves.SaveAsync()));
        Assert.Equal([completed], Directory.GetDirectories(fixture.BackupDirectory));
    }
}
