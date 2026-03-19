using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Server.Commands.WorldGen;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog.Events;

namespace Moongate.Tests.Server.Commands.WorldGen;

public sealed class InitialSpawnCommandTests
{
    private sealed class InitialSpawnTestSpawnService : ISpawnService
    {
        private readonly Dictionary<Serial, bool> _results;

        public InitialSpawnTestSpawnService(params (Serial ItemId, bool Result)[] configuredResults)
        {
            _results = configuredResults.ToDictionary(static entry => entry.ItemId, static entry => entry.Result);
        }

        public List<Serial> TriggeredIds { get; } = [];

        public int GetTrackedSpawnerCount()
            => 0;

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;

        public Task<bool> TriggerAsync(Serial spawnerItemId, CancellationToken cancellationToken = default)
        {
            TriggeredIds.Add(spawnerItemId);

            return Task.FromResult(_results.TryGetValue(spawnerItemId, out var result) ? result : true);
        }

        public Task<bool> TriggerAsync(UOItemEntity spawnerItem, CancellationToken cancellationToken = default)
            => TriggerAsync(spawnerItem.Id, cancellationToken);
    }

    private sealed class InitialSpawnTestPersistenceService : IPersistenceService
    {
        public InitialSpawnTestPersistenceService(IReadOnlyCollection<UOItemEntity> items)
        {
            UnitOfWork = new InitialSpawnTestPersistenceUnitOfWork(items);
        }

        public IPersistenceUnitOfWork UnitOfWork { get; }

        public void Dispose() { }

        public Task SaveAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class InitialSpawnTestPersistenceUnitOfWork : IPersistenceUnitOfWork
    {
        public InitialSpawnTestPersistenceUnitOfWork(IReadOnlyCollection<UOItemEntity> items)
        {
            Items = new InitialSpawnTestItemRepository(items);
            Accounts = new InitialSpawnUnusedAccountRepository();
            Mobiles = new InitialSpawnUnusedMobileRepository();
            BulletinBoardMessages = new InitialSpawnUnusedBulletinBoardMessageRepository();
        }

        public IAccountRepository Accounts { get; }

        public IMobileRepository Mobiles { get; }

        public IItemRepository Items { get; }

        public IBulletinBoardMessageRepository BulletinBoardMessages { get; }

        public Serial AllocateNextAccountId()
            => throw new NotSupportedException();

        public Serial AllocateNextItemId()
            => throw new NotSupportedException();

        public Serial AllocateNextMobileId()
            => throw new NotSupportedException();

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask SaveSnapshotAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class InitialSpawnUnusedBulletinBoardMessageRepository : IBulletinBoardMessageRepository
    {
        public ValueTask<IReadOnlyCollection<BulletinBoardMessageEntity>> GetAllAsync(
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyCollection<BulletinBoardMessageEntity>>([]);

        public ValueTask<IReadOnlyList<BulletinBoardMessageEntity>> GetByBoardIdAsync(
            Serial boardId,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyList<BulletinBoardMessageEntity>>([]);

        public ValueTask<BulletinBoardMessageEntity?> GetByIdAsync(
            Serial messageId,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<BulletinBoardMessageEntity?>(null);

        public ValueTask<bool> RemoveAsync(Serial messageId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);

        public ValueTask UpsertAsync(BulletinBoardMessageEntity message, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class InitialSpawnTestItemRepository : IItemRepository
    {
        private readonly IReadOnlyCollection<UOItemEntity> _items;

        public InitialSpawnTestItemRepository(IReadOnlyCollection<UOItemEntity> items)
        {
            _items = items;
        }

        public ValueTask BulkUpsertAsync(IReadOnlyList<UOItemEntity> items, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_items.Count);

        public ValueTask<IReadOnlyCollection<UOItemEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_items);

        public ValueTask<UOItemEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_items.FirstOrDefault(item => item.Id == id));

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOItemEntity, bool> predicate,
            Func<UOItemEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyList<TResult>>([.. _items.Where(predicate).Select(selector)]);

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);

        public ValueTask UpsertAsync(UOItemEntity item, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class ImmediateBackgroundJobService : IBackgroundJobService
    {
        public void EnqueueBackground(Action job)
            => job();

        public void EnqueueBackground(Func<Task> job)
            => job().GetAwaiter().GetResult();

        public int ExecutePendingOnGameLoop(int maxActions = 100)
            => 0;

        public void PostToGameLoop(Action action)
            => action();

        public void RunBackgroundAndPostResult<TResult>(
            Func<TResult> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
        {
            try
            {
                var result = backgroundJob();
                onGameLoopResult(result);
            }
            catch (Exception ex) when (onGameLoopError is not null)
            {
                onGameLoopError(ex);
            }
        }

        public void RunBackgroundAndPostResultAsync<TResult>(
            Func<Task<TResult>> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
        {
            try
            {
                var result = backgroundJob().GetAwaiter().GetResult();
                onGameLoopResult(result);
            }
            catch (Exception ex) when (onGameLoopError is not null)
            {
                onGameLoopError(ex);
            }
        }

        public void Start(int? workerCount = null) { }

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class InitialSpawnUnusedAccountRepository : IAccountRepository
    {
        public ValueTask<bool> AddAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<bool> ExistsAsync(
            Func<UOAccountEntity, bool> predicate,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyCollection<UOAccountEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<UOAccountEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<UOAccountEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOAccountEntity, bool> predicate,
            Func<UOAccountEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask UpsertAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class InitialSpawnUnusedMobileRepository : IMobileRepository
    {
        public ValueTask BulkUpsertAsync(
            IReadOnlyList<UOMobileEntity> mobiles,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyCollection<UOMobileEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<UOMobileEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOMobileEntity, bool> predicate,
            Func<UOMobileEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask UpsertAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    [Test]
    public async Task ExecuteCommandAsync_ShouldPrintProgressEveryFiveHundredSpawners()
    {
        var items = Enumerable
                    .Range(0, 500)
                    .Select(index => CreateWorldSpawner((Serial)(0x40000001u + (uint)index), 0))
                    .ToArray();
        var persistence = new InitialSpawnTestPersistenceService(items);
        var spawnService = new InitialSpawnTestSpawnService();
        var messages = new List<(string Message, LogEventLevel Level)>();
        var command = new InitialSpawnCommand(persistence, spawnService, new ImmediateBackgroundJobService());
        var context = new CommandSystemContext(
            "initial_spawn",
            [],
            CommandSourceType.Console,
            0,
            (message, level) => messages.Add((message, level))
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(
            messages.Select(static m => m.Message),
            Has.Some.Contains("Initial spawn progress: processed 500/500, triggered 500, skipped/failed 0")
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_ShouldTriggerAllPersistedSpawnerItems()
    {
        var spawnerA = CreateWorldSpawner((Serial)0x40000001u, 0);
        var spawnerB = CreateWorldSpawner((Serial)0x40000002u, 1);
        var nonSpawner = CreateWorldItem((Serial)0x40000003u, 0);
        var persistence = new InitialSpawnTestPersistenceService([spawnerA, spawnerB, nonSpawner]);
        var spawnService = new InitialSpawnTestSpawnService();
        var messages = new List<(string Message, LogEventLevel Level)>();
        var command = new InitialSpawnCommand(persistence, spawnService, new ImmediateBackgroundJobService());
        var context = new CommandSystemContext(
            "initial_spawn",
            [],
            CommandSourceType.Console,
            0,
            (message, level) => messages.Add((message, level))
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(spawnService.TriggeredIds, Is.EqualTo(new[] { spawnerA.Id, spawnerB.Id }));
                Assert.That(messages.Select(static m => m.Message), Has.Some.EqualTo("Starting initial spawn..."));
                Assert.That(
                    messages.Select(static m => m.Message),
                    Has.Some.Contains("Initial spawn complete: processed 2 spawners, triggered 2, skipped/failed 0")
                );
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenMapIdIsInvalid_ShouldWarnAndStop()
    {
        var persistence = new InitialSpawnTestPersistenceService([]);
        var spawnService = new InitialSpawnTestSpawnService();
        var messages = new List<(string Message, LogEventLevel Level)>();
        var command = new InitialSpawnCommand(persistence, spawnService, new ImmediateBackgroundJobService());
        var context = new CommandSystemContext(
            "initial_spawn nope",
            ["nope"],
            CommandSourceType.Console,
            0,
            (message, level) => messages.Add((message, level))
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(spawnService.TriggeredIds, Is.Empty);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Level, Is.EqualTo(LogEventLevel.Warning));
                Assert.That(messages[0].Message, Is.EqualTo("Usage: .initial_spawn [mapId]"));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenMapIdProvided_ShouldTriggerOnlyMatchingMapSpawners()
    {
        var map0Spawner = CreateWorldSpawner((Serial)0x40000001u, 0);
        var map1Spawner = CreateWorldSpawner((Serial)0x40000002u, 1);
        var persistence = new InitialSpawnTestPersistenceService([map0Spawner, map1Spawner]);
        var spawnService = new InitialSpawnTestSpawnService();
        var command = new InitialSpawnCommand(persistence, spawnService, new ImmediateBackgroundJobService());
        var context = new CommandSystemContext(
            "initial_spawn 1",
            ["1"],
            CommandSourceType.Console,
            0,
            static (_, _) => { }
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(spawnService.TriggeredIds, Is.EqualTo(new[] { map1Spawner.Id }));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenTriggerReturnsFalse_ShouldCountAsSkippedOrFailed()
    {
        var spawnerA = CreateWorldSpawner((Serial)0x40000001u, 0);
        var spawnerB = CreateWorldSpawner((Serial)0x40000002u, 0);
        var persistence = new InitialSpawnTestPersistenceService([spawnerA, spawnerB]);
        var spawnService = new InitialSpawnTestSpawnService((spawnerB.Id, false));
        var messages = new List<(string Message, LogEventLevel Level)>();
        var command = new InitialSpawnCommand(persistence, spawnService, new ImmediateBackgroundJobService());
        var context = new CommandSystemContext(
            "initial_spawn",
            [],
            CommandSourceType.Console,
            0,
            (message, level) => messages.Add((message, level))
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(
            messages.Select(static m => m.Message),
            Has.Some.Contains("Initial spawn complete: processed 2 spawners, triggered 1, skipped/failed 1")
        );
    }

    private static UOItemEntity CreateWorldItem(Serial id, int mapId)
        => new()
        {
            Id = id,
            MapId = mapId,
            ItemId = 0x1F13,
            Name = "Spawner",
            Location = new(100, 100, 0),
            ParentContainerId = Serial.Zero,
            EquippedMobileId = Serial.Zero
        };

    private static UOItemEntity CreateWorldSpawner(Serial id, int mapId)
    {
        var item = CreateWorldItem(id, mapId);
        item.SetCustomString("spawner_id", Guid.NewGuid().ToString("D"));

        return item;
    }
}
