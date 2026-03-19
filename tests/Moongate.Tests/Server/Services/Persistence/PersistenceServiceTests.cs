using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.EventLoop;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Services.Persistence;

public class PersistenceServiceTests
{
    private sealed class TimerServiceSpy : ITimerService
    {
        public TimeSpan? LastInterval { get; private set; }
        public Action? LastCallback { get; private set; }

        public void ProcessTick() { }

        public string RegisterTimer(
            string name,
            TimeSpan interval,
            Action callback,
            TimeSpan? delay = null,
            bool repeat = false
        )
        {
            LastInterval = interval;
            LastCallback = callback;

            return "timer-spy";
        }

        public void UnregisterAllTimers() { }

        public bool UnregisterTimer(string timerId)
            => true;

        public int UnregisterTimersByName(string name)
            => 0;

        public int UpdateTicksDelta(long timestampMilliseconds)
            => 0;
    }

    private sealed class BackgroundJobServiceSpy : IBackgroundJobService
    {
        private readonly Queue<Func<Task>> _queued = new();

        public int EnqueuedJobsCount => _queued.Count;

        public void EnqueueBackground(Action job)
        {
            ArgumentNullException.ThrowIfNull(job);
            _queued.Enqueue(
                () =>
                {
                    job();

                    return Task.CompletedTask;
                }
            );
        }

        public void EnqueueBackground(Func<Task> job)
        {
            ArgumentNullException.ThrowIfNull(job);
            _queued.Enqueue(job);
        }

        public int ExecutePendingOnGameLoop(int maxActions = 100)
            => 0;

        public void PostToGameLoop(Action action) { }

        public void RunBackgroundAndPostResult<TResult>(
            Func<TResult> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
            => throw new NotSupportedException();

        public void RunBackgroundAndPostResultAsync<TResult>(
            Func<Task<TResult>> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
            => throw new NotSupportedException();

        public async Task RunNextAsync()
        {
            if (_queued.Count == 0)
            {
                throw new InvalidOperationException("No queued jobs.");
            }

            await _queued.Dequeue()();
        }

        public void Start(int? workerCount = null) { }

        public Task StopAsync()
            => Task.CompletedTask;
    }

    [Test]
    public async Task SaveAsync_ShouldUpdateSnapshotMetrics()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        using var service = CreatePersistenceService(directories);

        await service.StartAsync();
        await service.SaveAsync();
        var snapshot = service.GetMetricsSnapshot();

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshot.TotalSaves, Is.GreaterThanOrEqualTo(1));
                Assert.That(snapshot.LastSaveDurationMs, Is.GreaterThanOrEqualTo(0));
                Assert.That(snapshot.LastSaveTimestampUtc, Is.Not.Null);
                Assert.That(snapshot.SaveErrors, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task SaveAsync_ShouldWriteSnapshotFileInSaveDirectory()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        using var service = CreatePersistenceService(directories);

        await service.StartAsync();
        await service.SaveAsync();

        var snapshotPath = Path.Combine(directories[DirectoryType.Save], "world.snapshot.bin");
        Assert.That(File.Exists(snapshotPath), Is.True);
    }

    [Test]
    public async Task ScheduledAutosaveBackgroundJob_ShouldCompleteSaveAndUpdateMetrics()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var timerSpy = new TimerServiceSpy();
        var backgroundSpy = new BackgroundJobServiceSpy();
        using var service = new PersistenceService(
            directories,
            timerSpy,
            backgroundSpy,
            new(),
            new NetworkServiceTestGameEventBusService()
        );

        await service.StartAsync();
        timerSpy.LastCallback!.Invoke();
        await backgroundSpy.RunNextAsync();

        var snapshot = service.GetMetricsSnapshot();

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshot.TotalSaves, Is.GreaterThanOrEqualTo(1));
                Assert.That(snapshot.LastSaveTimestampUtc, Is.Not.Null);
                Assert.That(File.Exists(Path.Combine(directories[DirectoryType.Save], "world.snapshot.bin")), Is.True);
            }
        );
    }

    [Test]
    public async Task StartAsync_AndStopAsync_ShouldPersistDataAcrossRestart()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());

        using var first = CreatePersistenceService(directories);
        await first.StartAsync();

        await first.UnitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000033,
                Username = "persist-user",
                PasswordHash = "pw"
            }
        );

        await first.StopAsync();
        first.Dispose();

        using var second = CreatePersistenceService(directories);
        await second.StartAsync();

        var loaded = await second.UnitOfWork.Accounts.GetByUsernameAsync("persist-user");

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Id, Is.EqualTo((Serial)0x00000033));
    }

    [Test]
    public async Task StartAsync_ShouldUseConfiguredSaveInterval()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var timerSpy = new TimerServiceSpy();
        var config = new MoongateConfig
        {
            Persistence = new()
            {
                SaveIntervalSeconds = 12
            }
        };

        using var service = new PersistenceService(
            directories,
            timerSpy,
            new BackgroundJobServiceSpy(),
            config,
            new NetworkServiceTestGameEventBusService()
        );
        await service.StartAsync();

        Assert.That(timerSpy.LastInterval, Is.EqualTo(TimeSpan.FromSeconds(12)));
    }

    [Test]
    public async Task StartAsync_TimerCallback_ShouldEnqueueAutosaveInsteadOfRunningInline()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var timerSpy = new TimerServiceSpy();
        var backgroundSpy = new BackgroundJobServiceSpy();
        using var service = new PersistenceService(
            directories,
            timerSpy,
            backgroundSpy,
            new(),
            new NetworkServiceTestGameEventBusService()
        );

        await service.StartAsync();
        timerSpy.LastCallback!.Invoke();

        Assert.That(backgroundSpy.EnqueuedJobsCount, Is.EqualTo(1));
        Assert.That(service.GetMetricsSnapshot().TotalSaves, Is.EqualTo(0));
    }

    [Test]
    public async Task StartAsync_TimerCallback_ShouldSkipSchedulingWhenAutosaveAlreadyInFlight()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var timerSpy = new TimerServiceSpy();
        var backgroundSpy = new BackgroundJobServiceSpy();
        using var service = new PersistenceService(
            directories,
            timerSpy,
            backgroundSpy,
            new(),
            new NetworkServiceTestGameEventBusService()
        );

        await service.StartAsync();

        timerSpy.LastCallback!.Invoke();
        timerSpy.LastCallback.Invoke();

        Assert.That(backgroundSpy.EnqueuedJobsCount, Is.EqualTo(1));
    }

    private static PersistenceService CreatePersistenceService(DirectoriesConfig directoriesConfig)
        => new(
            directoriesConfig,
            new TimerWheelService(
                new()
                {
                    TickDuration = TimeSpan.FromMilliseconds(250),
                    WheelSize = 512
                }
            ),
            new BackgroundJobService(),
            new(),
            new NetworkServiceTestGameEventBusService()
        );
}
