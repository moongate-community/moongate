using Moongate.Core.Directories;
using Moongate.Core.Primitives;
using Moongate.Persistence.DataAccess;
using Moongate.Persistence.Services;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Persistence;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.Support.Persistence;

namespace Moongate.Tests.TestSupport.Persistence;

internal sealed class WorldSaveFixture : IAsyncDisposable
{
    private readonly TemporaryPersistenceDirectory _root = new();
    private readonly DirectoriesConfig _directories;

    public string SaveDirectory => Path.Combine(_root.Path, "save");

    /// <summary>Where the service actually writes backups: it derives this from the root, ignoring the option.</summary>
    public string BackupDirectory => Path.Join(_root.Path, "world-saves");
    public WorldSaveTimeProvider Clock { get; } = new();
    public ControlledWorldSaveFileSystem FileSystem { get; } = new();
    public MoongatePersistenceService Persistence { get; }
    public TimerWheelService Timers { get; }
    public GameLoopService Loop { get; }
    public WorldSaveService Saves { get; }
    public DataAccess<TestEntity> Items { get; }
    public List<TestEntity> Entities { get; } = [new() { Id = new Serial(7), Name = "before" }];
    public int Captures { get; private set; }
    public Exception? CaptureFailure { get; set; }

    public WorldSaveFixture(bool autosave = false, bool backups = false, int retention = 5)
    {
        // Passing no directory names keeps Init from pre-creating the backup root, which several
        // tests assert is absent until a backup is actually published.
        _directories = new DirectoriesConfig(_root.Path, []);
        Persistence = new MoongatePersistenceService(SaveDirectory);
        Timers = new TimerWheelService(new TimerWheelOptions(), Clock);
        Loop = new GameLoopService(new GameLoopOptions(), Timers, Clock);
        Items = Persistence.Register<TestEntity>("items", () =>
        {
            Assert.True(Loop.IsOnLoopThread);
            Captures++;
            if (CaptureFailure is not null)
            {
                throw CaptureFailure;
            }
            return Entities;
        });
        FileSystem.Attach(Items);
        Saves = new WorldSaveService(Persistence, Loop, Timers, new WorldSaveOptions
        {
            Enabled = autosave, Interval = TimeSpan.FromSeconds(2), BackupsEnabled = backups,
            BackupRetentionCount = retention
        }, Clock, _directories);
    }

    public async Task StartAsync(bool activate = true)
    {
        await Persistence.InitializeAsync();
        await Loop.StartAsync();
        await Saves.StartAsync();
        if (activate)
        {
            Saves.Activate();
        }
    }

    public async Task OnLoopAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Loop.PostAsync(new ActionGameLoopWorkItem(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        }));
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public async Task<string?> ReadSavedNameAsync()
    {
        await Persistence.DisposeAsync();
        await using var reopened = new MoongatePersistenceService(SaveDirectory);
        var items = reopened.Register<TestEntity>("items");
        await reopened.InitializeAsync();
        return items.GetById(new Serial(7))?.Name;
    }

    public async ValueTask DisposeAsync()
    {
        FileSystem.Release();
        FileSystem.FlushFailure = null;
        await Saves.StopAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        Loop.Dispose();
        await Persistence.DisposeAsync().AsTask().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        FileSystem.Dispose();
        _root.Dispose();
    }
}
