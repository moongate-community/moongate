using BenchmarkDotNet.Attributes;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Services.Items;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Benchmarks;

[MemoryDiagnoser]
public class ItemServiceBenchmark : IDisposable
{
    private InMemoryPersistenceService _persistenceService = null!;
    private ItemService _itemService = null!;

    private Serial _containerAId;
    private Serial _containerBId;
    private Serial _movingItemId;
    private Serial _dropItemId;

    private sealed class NoOpGameEventBusService : IGameEventBusService
    {
        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
            => ValueTask.CompletedTask;

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent { }
    }

    private sealed class InMemoryPersistenceService : IPersistenceService
    {
        public InMemoryPersistenceService()
        {
            UnitOfWork = new InMemoryPersistenceUnitOfWork();
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

    private sealed class InMemoryPersistenceUnitOfWork : IPersistenceUnitOfWork
    {
        private uint _nextAccountId = 1;
        private uint _nextItemId = 0x5000_0000;
        private uint _nextMobileId = 1;

        public InMemoryPersistenceUnitOfWork()
        {
            Accounts = new InMemoryAccountRepository();
            Mobiles = new InMemoryMobileRepository();
            Items = new InMemoryItemRepository();
            BulletinBoardMessages = new InMemoryBulletinBoardMessageRepository();
        }

        public IAccountRepository Accounts { get; }

        public IMobileRepository Mobiles { get; }

        public IItemRepository Items { get; }

        public IBulletinBoardMessageRepository BulletinBoardMessages { get; }

        public Serial AllocateNextAccountId()
            => (Serial)_nextAccountId++;

        public Serial AllocateNextItemId()
            => (Serial)_nextItemId++;

        public Serial AllocateNextMobileId()
            => (Serial)_nextMobileId++;

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask SaveSnapshotAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class InMemoryBulletinBoardMessageRepository : IBulletinBoardMessageRepository
    {
        public ValueTask<IReadOnlyCollection<BulletinBoardMessageEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<BulletinBoardMessageEntity>>([]);

        public ValueTask<BulletinBoardMessageEntity?> GetByIdAsync(Serial messageId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<BulletinBoardMessageEntity?>(null);

        public ValueTask<IReadOnlyList<BulletinBoardMessageEntity>> GetByBoardIdAsync(Serial boardId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<BulletinBoardMessageEntity>>([]);

        public ValueTask UpsertAsync(BulletinBoardMessageEntity message, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<bool> RemoveAsync(Serial messageId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);
    }

    private sealed class InMemoryItemRepository : IItemRepository
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = [];

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_items.Count);

        public ValueTask<IReadOnlyCollection<UOItemEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<UOItemEntity>>(_items.Values.ToList());

        public ValueTask<UOItemEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _items.TryGetValue(id, out var item);

            return ValueTask.FromResult(item);
        }

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOItemEntity, bool> predicate,
            Func<UOItemEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
        {
            var values = _items.Values.Where(predicate).Select(selector).ToList();

            return ValueTask.FromResult<IReadOnlyList<TResult>>(values);
        }

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_items.Remove(id));

        public ValueTask UpsertAsync(UOItemEntity item, CancellationToken cancellationToken = default)
        {
            _items[item.Id] = item;

            return ValueTask.CompletedTask;
        }

        public ValueTask BulkUpsertAsync(IReadOnlyList<UOItemEntity> items, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class InMemoryMobileRepository : IMobileRepository
    {
        private readonly Dictionary<Serial, UOMobileEntity> _mobiles = [];

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_mobiles.Count);

        public ValueTask<IReadOnlyCollection<UOMobileEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<UOMobileEntity>>(_mobiles.Values.ToList());

        public ValueTask<UOMobileEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _mobiles.TryGetValue(id, out var mobile);

            return ValueTask.FromResult(mobile);
        }

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOMobileEntity, bool> predicate,
            Func<UOMobileEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
        {
            var values = _mobiles.Values.Where(predicate).Select(selector).ToList();

            return ValueTask.FromResult<IReadOnlyList<TResult>>(values);
        }

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_mobiles.Remove(id));

        public ValueTask UpsertAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _mobiles[mobile.Id] = mobile;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class InMemoryAccountRepository : IAccountRepository
    {
        private readonly Dictionary<Serial, UOAccountEntity> _accounts = [];

        public ValueTask<bool> AddAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
        {
            if (_accounts.ContainsKey(account.Id) || _accounts.Values.Any(existing => existing.Username == account.Username))
            {
                return ValueTask.FromResult(false);
            }

            _accounts[account.Id] = account;

            return ValueTask.FromResult(true);
        }

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_accounts.Count);

        public ValueTask<bool> ExistsAsync(
            Func<UOAccountEntity, bool> predicate,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult(_accounts.Values.Any(predicate));

        public ValueTask<IReadOnlyCollection<UOAccountEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<UOAccountEntity>>(_accounts.Values.ToList());

        public ValueTask<UOAccountEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _accounts.TryGetValue(id, out var account);

            return ValueTask.FromResult(account);
        }

        public ValueTask<UOAccountEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            var account = _accounts.Values.FirstOrDefault(existing => existing.Username == username);

            return ValueTask.FromResult(account);
        }

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOAccountEntity, bool> predicate,
            Func<UOAccountEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
        {
            var values = _accounts.Values.Where(predicate).Select(selector).ToList();

            return ValueTask.FromResult<IReadOnlyList<TResult>>(values);
        }

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_accounts.Remove(id));

        public ValueTask UpsertAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
        {
            _accounts[account.Id] = account;

            return ValueTask.CompletedTask;
        }
    }

    public void Dispose()
    {
        _persistenceService.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public int DropItemToGroundFromContainer()
    {
        var success = 0;

        for (var i = 0; i < 100; i++)
        {
            _itemService
                .MoveItemToContainerAsync(_dropItemId, _containerAId, new(1, 1))
                .GetAwaiter()
                .GetResult();

            var dropped = _itemService
                          .DropItemToGroundAsync(_dropItemId, new(1450 + i % 8, 1670 + i % 8, 0), 0)
                          .GetAwaiter()
                          .GetResult();

            if (dropped is not null)
            {
                success++;
            }
        }

        return success;
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public int MoveItemBetweenContainers()
    {
        var success = 0;
        var toA = false;

        for (var i = 0; i < 100; i++)
        {
            var targetContainer = toA ? _containerAId : _containerBId;
            var moved = _itemService
                        .MoveItemToContainerAsync(_movingItemId, targetContainer, new(i % 40, i % 30))
                        .GetAwaiter()
                        .GetResult();

            if (moved)
            {
                success++;
            }

            toA = !toA;
        }

        return success;
    }

    [GlobalSetup]
    public async Task Setup()
    {
        _persistenceService = new();
        _itemService = new(_persistenceService, new NoOpGameEventBusService());

        _containerAId = (Serial)0x4000_0010;
        _containerBId = (Serial)0x4000_0020;
        _movingItemId = (Serial)0x4000_0100;
        _dropItemId = (Serial)0x4000_0200;

        var containerA = CreateContainer(_containerAId, 0x0E75);
        var containerB = CreateContainer(_containerBId, 0x0E76);
        var movingItem = CreateItem(_movingItemId, 0x0E21);
        var dropItem = CreateItem(_dropItemId, 0x0EED);

        containerA.AddItem(movingItem, new(1, 1));
        containerA.AddItem(dropItem, new(2, 2));

        await _persistenceService.UnitOfWork.Items.UpsertAsync(containerA);
        await _persistenceService.UnitOfWork.Items.UpsertAsync(containerB);
        await _persistenceService.UnitOfWork.Items.UpsertAsync(movingItem);
        await _persistenceService.UnitOfWork.Items.UpsertAsync(dropItem);
    }

    private static UOItemEntity CreateContainer(Serial id, int itemId)
        => new()
        {
            Id = id,
            ItemId = itemId,
            IsStackable = false,
            Location = new(1450, 1670, 0),
            MapId = 0,
            Amount = 1,
            Hue = 0,
            Direction = DirectionType.North,
            ScriptId = "bench_container"
        };

    private static UOItemEntity CreateItem(Serial id, int itemId)
        => new()
        {
            Id = id,
            ItemId = itemId,
            IsStackable = false,
            Location = new(0, 0, 0),
            MapId = 0,
            Amount = 1,
            Hue = 0,
            Direction = DirectionType.North,
            ScriptId = "bench_item"
        };
}
