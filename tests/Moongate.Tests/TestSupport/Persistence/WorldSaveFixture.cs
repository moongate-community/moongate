using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Persistence.DataAccess;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Services;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Persistence;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Support.GameLoop;
using Npgsql;

namespace Moongate.Tests.TestSupport.Persistence;

internal sealed class WorldSaveFixture : IAsyncDisposable
{
    private readonly HostPersistenceFixture _host;
    private NpgsqlConnection? _blocker;
    private NpgsqlTransaction? _blockingTransaction;
    public PostgreSqlTestDatabase Database => _host.Database;
    public PostgreSqlTestDatabase? AccountsDatabase => _host.AccountsDatabase;
    public PersistenceOperationBarrier Operations { get; } = new();
    private PostgreSqlTestDatabase? _blockedDatabase;
    private string _blockedTable = "host_test.items";
    public WorldSaveTimeProvider Clock { get; } = new();
    public MoongatePersistenceService Persistence => _host.Owner;
    public TimerWheelService Timers { get; }
    public GameLoopService Loop { get; }
    public WorldSaveService Saves { get; }
    public DataAccess<TestEntity> Items => _host.Container.Resolve<DataAccess<TestEntity>>();
    public List<TestEntity> Entities { get; } = [new() { Id = new Serial(7), Name = "before" }];
    public int Captures { get; private set; }
    public Exception? CaptureFailure { get; set; }
    public Action? OnCapture { get; set; }

    private WorldSaveFixture(HostPersistenceFixture host, bool autosave)
    {
        _host = host;
        Timers = new TimerWheelService(new TimerWheelOptions(), Clock);
        Loop = new GameLoopService(new GameLoopOptions(), Timers, Clock);
        host.Container.AddPersistenceModule<TestPersistenceModule>()
            .AddPersistenceEntity<TestEntity>(
                () =>
                {
                    Assert.True(Loop.IsOnLoopThread);
                    Captures++;
                    OnCapture?.Invoke();
                    if (CaptureFailure is not null)
                    {
                        throw CaptureFailure;
                    }

                    return Entities;
                },
                entity => new TestEntity { Id = entity.Id, Name = entity.Name }
            );
        if (host.AccountsDatabase is not null)
        {
            host.Container.AddPersistenceModule<AccountSnapshotModule>()
                .AddPersistenceEntity<AccountSnapshotEntity>(
                    () =>
                    {
                        Assert.True(Loop.IsOnLoopThread);
                        Captures++;
                        return [new AccountSnapshotEntity { Id = new Serial(8), Name = "account" }];
                    },
                    entity => new AccountSnapshotEntity { Id = entity.Id, Name = entity.Name }
                );
        }

        Saves = new WorldSaveService(
            Persistence,
            Loop,
            Timers,
            new WorldSaveOptions
            {
                Enabled = autosave, Interval = TimeSpan.FromSeconds(2)
            },
            Clock,
            Operations
        );
    }

    public static async Task<WorldSaveFixture> CreateAsync(bool autosave = false, bool twoTargets = false)
    {
        return new WorldSaveFixture(await HostPersistenceFixture.CreateAsync(twoTargets: twoTargets), autosave);
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

    public async Task BlockWritesAsync(bool accounts = false)
    {
        _blockedDatabase = accounts ? AccountsDatabase! : Database;
        _blockedTable = accounts ? "host_accounts.items" : "host_test.items";
        _blocker = new NpgsqlConnection(_blockedDatabase.ConnectionString);
        await _blocker.OpenAsync();
        _blockingTransaction = await _blocker.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            $"LOCK TABLE {_blockedTable} IN ACCESS EXCLUSIVE MODE",
            _blocker,
            _blockingTransaction
        );
        await command.ExecuteNonQueryAsync();
    }

    public async Task WaitForBlockedWriteAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!await _blockedDatabase!.ScalarAsync<bool>(
                   $"SELECT EXISTS (SELECT 1 FROM pg_locks WHERE relation = '{_blockedTable}'::regclass AND NOT granted)"
               ))
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    public async Task ReleaseWritesAsync()
    {
        if (_blockingTransaction is not null)
        {
            await _blockingTransaction.RollbackAsync();
            await _blockingTransaction.DisposeAsync();
            _blockingTransaction = null;
        }

        if (_blocker is not null)
        {
            await _blocker.DisposeAsync();
            _blocker = null;
        }
    }

    public async Task OnLoopAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Loop.PostAsync(
            new ActionGameLoopWorkItem(() =>
                {
                    try
                    {
                        action();
                        completion.SetResult();
                    }
                    catch (Exception exception)
                    {
                        completion.SetException(exception);
                    }
                }
            )
        );
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public Task<string?> ReadSavedNameAsync()
    {
        return Database.ScalarAsync<string>("SELECT name FROM host_test.items WHERE id = 7");
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseWritesAsync();
        await Saves.StopAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        Loop.Dispose();
        await _host.DisposeAsync();
    }
}
