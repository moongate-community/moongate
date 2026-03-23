using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Constants;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.World;

public sealed class CorpseStartupCleanupServiceTests
{
    private sealed class NoOpAccountRepository : IAccountRepository
    {
        public ValueTask<bool> AddAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(0);

        public ValueTask<bool> ExistsAsync(
            Func<UOAccountEntity, bool> predicate,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult(false);

        public ValueTask<IReadOnlyCollection<UOAccountEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<UOAccountEntity>>([]);

        public ValueTask<UOAccountEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<UOAccountEntity?>(null);

        public ValueTask<UOAccountEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<UOAccountEntity?>(null);

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOAccountEntity, bool> predicate,
            Func<UOAccountEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyList<TResult>>([]);

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);

        public ValueTask UpsertAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class NoOpBulletinBoardMessageRepository : IBulletinBoardMessageRepository
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

    private sealed class TestMobileRepository : IMobileRepository
    {
        private readonly Dictionary<Serial, UOMobileEntity> _mobiles = [];

        public List<Serial> UpsertCalls { get; } = [];

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_mobiles.Count);

        public ValueTask<IReadOnlyCollection<UOMobileEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<UOMobileEntity>>([.. _mobiles.Values]);

        public ValueTask<UOMobileEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_mobiles.TryGetValue(id, out var mobile) ? mobile : null);

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOMobileEntity, bool> predicate,
            Func<UOMobileEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyList<TResult>>([.. _mobiles.Values.Where(predicate).Select(selector)]);

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_mobiles.Remove(id));

        public void Seed(IEnumerable<UOMobileEntity> mobiles)
        {
            foreach (var mobile in mobiles)
            {
                _mobiles[mobile.Id] = mobile;
            }
        }

        public ValueTask UpsertAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            UpsertCalls.Add(mobile.Id);
            _mobiles[mobile.Id] = mobile;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoOpHelpTicketRepository : IHelpTicketRepository
    {
        public ValueTask<IReadOnlyCollection<HelpTicketEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<HelpTicketEntity>>([]);

        public ValueTask<HelpTicketEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<HelpTicketEntity?>(null);

        public ValueTask<IReadOnlyList<HelpTicketEntity>> GetBySenderCharacterIdAsync(
            Serial senderCharacterId,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyList<HelpTicketEntity>>([]);

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);

        public ValueTask UpsertAsync(HelpTicketEntity entity, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class TestItemRepository : IItemRepository
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = [];
        private readonly HashSet<Serial> _removeFailureIds = [];

        public List<Serial> RemoveCalls { get; } = [];
        public List<Serial> UpsertCalls { get; } = [];

        public ValueTask BulkUpsertAsync(IReadOnlyList<UOItemEntity> items, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public bool Contains(Serial id)
            => _items.ContainsKey(id);

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_items.Count);

        public void FailRemove(Serial id)
            => _removeFailureIds.Add(id);

        public ValueTask<IReadOnlyCollection<UOItemEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<UOItemEntity>>([.. _items.Values]);

        public ValueTask<UOItemEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_items.TryGetValue(id, out var item) ? item : null);

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOItemEntity, bool> predicate,
            Func<UOItemEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyList<TResult>>([.. _items.Values.Where(predicate).Select(selector)]);

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
        {
            RemoveCalls.Add(id);

            if (_removeFailureIds.Contains(id))
            {
                return ValueTask.FromResult(false);
            }

            return ValueTask.FromResult(_items.Remove(id));
        }

        public void Seed(IEnumerable<UOItemEntity> items)
        {
            foreach (var item in items)
            {
                _items[item.Id] = item;
            }
        }

        public ValueTask UpsertAsync(UOItemEntity item, CancellationToken cancellationToken = default)
        {
            UpsertCalls.Add(item.Id);
            _items[item.Id] = item;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestPersistenceService : IPersistenceService
    {
        public TestPersistenceService(
            TestItemRepository items,
            TestMobileRepository mobiles
        )
        {
            TestUnitOfWork = new(items, mobiles);
        }

        public int SaveCallCount { get; private set; }

        public TestPersistenceUnitOfWork TestUnitOfWork { get; }

        IPersistenceUnitOfWork IPersistenceService.UnitOfWork => TestUnitOfWork;

        public void Dispose() { }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            SaveCallCount++;

            return Task.CompletedTask;
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class TestPersistenceUnitOfWork : IPersistenceUnitOfWork
    {
        public TestPersistenceUnitOfWork(
            TestItemRepository items,
            TestMobileRepository mobiles
        )
        {
            Accounts = new NoOpAccountRepository();
            Mobiles = mobiles;
            Items = items;
            BulletinBoardMessages = new NoOpBulletinBoardMessageRepository();
            HelpTickets = new NoOpHelpTicketRepository();
        }

        public IAccountRepository Accounts { get; }

        public TestMobileRepository Mobiles { get; }

        public TestItemRepository Items { get; }

        public IBulletinBoardMessageRepository BulletinBoardMessages { get; }

        public IHelpTicketRepository HelpTickets { get; }

        IItemRepository IPersistenceUnitOfWork.Items => Items;

        IMobileRepository IPersistenceUnitOfWork.Mobiles => Mobiles;

        public Serial AllocateNextAccountId()
            => (Serial)1u;

        public Serial AllocateNextItemId()
            => (Serial)1u;

        public Serial AllocateNextMobileId()
            => (Serial)1u;

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask SaveSnapshotAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    [Test]
    public async Task StartAsync_WhenCorpseHasDeepLinearContents_ShouldDeleteAllDescendantsAndRoot()
    {
        var chainLength = 256;
        var items = new List<UOItemEntity>(chainLength);

        var corpse = CreateCorpse((Serial)0x40001100u);
        items.Add(corpse);

        var parent = corpse;

        for (var index = 1; index < chainLength; index++)
        {
            var id = (Serial)((uint)corpse.Id + (uint)index);
            var item = CreateItem(id, 0x0AD0 + index, false, parent.Id);
            parent.AddItem(item, Point2D.Zero);
            items.Add(item);
            parent = item;
        }

        var persistence = CreatePersistenceService(items.ToArray());
        var service = new CorpseStartupCleanupService(persistence);

        await service.StartAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(persistence.SaveCallCount, Is.EqualTo(1));
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls.Count, Is.EqualTo(chainLength));
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls.First(), Is.EqualTo(items[^1].Id));
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls.Last(), Is.EqualTo(corpse.Id));
                Assert.That(persistence.TestUnitOfWork.Items.Contains(corpse.Id), Is.False);
                Assert.That(persistence.TestUnitOfWork.Items.Contains(parent.Id), Is.False);
            }
        );
    }

    [Test]
    public async Task StartAsync_WhenCorpseHasRecursiveContents_ShouldDeleteChildrenAndRoot()
    {
        var corpse = CreateCorpse((Serial)0x40001020u);
        var chest = CreateItem((Serial)0x40001021u, 0x0ABD, false, corpse.Id);
        var pouch = CreateItem((Serial)0x40001022u, 0x0ABE, false, chest.Id);
        corpse.AddItem(chest, Point2D.Zero);
        chest.AddItem(pouch, Point2D.Zero);

        var persistence = CreatePersistenceService(corpse, chest, pouch);
        var service = new CorpseStartupCleanupService(persistence);

        await service.StartAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Is.EqualTo([pouch.Id, chest.Id, corpse.Id]));
                Assert.That(persistence.SaveCallCount, Is.EqualTo(1));
                Assert.That(persistence.TestUnitOfWork.Items.Contains(corpse.Id), Is.False);
                Assert.That(persistence.TestUnitOfWork.Items.Contains(chest.Id), Is.False);
                Assert.That(persistence.TestUnitOfWork.Items.Contains(pouch.Id), Is.False);
            }
        );
    }

    [Test]
    public async Task StartAsync_WhenCorpseIsEquippedBySurvivingMobile_ShouldDetachEquipmentReferenceBeforeDelete()
    {
        var owner = new UOMobileEntity
        {
            Id = (Serial)0x00002000u,
            Name = "Guard Owner"
        };
        var corpse = CreateCorpse((Serial)0x40001073u);
        owner.EquipItem(ItemLayerType.Cloak, corpse);

        var persistence = CreatePersistenceService([corpse], [owner]);
        var service = new CorpseStartupCleanupService(persistence);

        await service.StartAsync();

        var persistedOwner = await persistence.TestUnitOfWork.Mobiles.GetByIdAsync(owner.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(persistence.SaveCallCount, Is.EqualTo(1));
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Is.EqualTo([corpse.Id]));
                Assert.That(persistence.TestUnitOfWork.Mobiles.UpsertCalls, Contains.Item(owner.Id));
                Assert.That(persistence.TestUnitOfWork.Items.Contains(corpse.Id), Is.False);
                Assert.That(persistedOwner, Is.Not.Null);
                Assert.That(persistedOwner!.HasEquippedItem(ItemLayerType.Cloak), Is.False);
            }
        );
    }

    [Test]
    public async Task StartAsync_WhenCorpseIsInsideSurvivingContainer_ShouldDetachParentReferenceBeforeDelete()
    {
        var parentContainer = CreateItem((Serial)0x40001070u, 0x0AC3, false);
        var corpse = CreateCorpse((Serial)0x40001071u);
        parentContainer.AddItem(corpse, Point2D.Zero);

        var persistence = CreatePersistenceService(parentContainer, corpse);
        var service = new CorpseStartupCleanupService(persistence);

        await service.StartAsync();

        var persistedParent = await persistence.TestUnitOfWork.Items.GetByIdAsync(parentContainer.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(persistence.SaveCallCount, Is.EqualTo(1));
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Is.EqualTo([corpse.Id]));
                Assert.That(persistence.TestUnitOfWork.Items.UpsertCalls, Contains.Item(parentContainer.Id));
                Assert.That(persistence.TestUnitOfWork.Items.Contains(corpse.Id), Is.False);
                Assert.That(persistedParent, Is.Not.Null);
                Assert.That(persistedParent!.ContainedItemIds, Does.Not.Contain(corpse.Id));
            }
        );
    }

    [Test]
    public async Task StartAsync_WhenCorpseIsPersistedInsideContainer_ShouldDetachParentContainerBeforeRemoval()
    {
        var container = CreateItem((Serial)0x40001070u, 0x0AC3, false);
        var corpse = CreateCorpse((Serial)0x40001071u);
        var child = CreateItem((Serial)0x40001072u, 0x0AC4, false, corpse.Id);

        container.AddItem(corpse, Point2D.Zero);
        corpse.AddItem(child, Point2D.Zero);

        var persistence = CreatePersistenceService(container, corpse, child);
        var service = new CorpseStartupCleanupService(persistence);

        await service.StartAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Is.EqualTo([child.Id, corpse.Id]));
                Assert.That(persistence.TestUnitOfWork.Items.UpsertCalls, Contains.Item(container.Id));
                Assert.That(container.ContainedItemIds, Does.Not.Contain(corpse.Id));
                Assert.That(persistence.SaveCallCount, Is.EqualTo(1));
                Assert.That(persistence.TestUnitOfWork.Items.Contains(container.Id), Is.True);
                Assert.That(persistence.TestUnitOfWork.Items.Contains(corpse.Id), Is.False);
                Assert.That(persistence.TestUnitOfWork.Items.Contains(child.Id), Is.False);
            }
        );
    }

    [Test]
    public async Task StartAsync_WhenMultipleCorpseRootsExist_ShouldRemoveAllCorpsesAndSaveOnce()
    {
        var corpseOne = CreateCorpse((Serial)0x40001030u);
        var corpseOneChild = CreateItem((Serial)0x40001031u, 0x0ABF, false, corpseOne.Id);
        corpseOne.AddItem(corpseOneChild, Point2D.Zero);

        var corpseTwo = CreateCorpse((Serial)0x40001040u);
        var corpseTwoChild = CreateItem((Serial)0x40001041u, 0x0AC0, false, corpseTwo.Id);
        corpseTwo.AddItem(corpseTwoChild, Point2D.Zero);

        var persistence = CreatePersistenceService(corpseOne, corpseOneChild, corpseTwo, corpseTwoChild);
        var service = new CorpseStartupCleanupService(persistence);

        await service.StartAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Contains.Item(corpseOneChild.Id));
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Contains.Item(corpseTwoChild.Id));
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Contains.Item(corpseOne.Id));
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Contains.Item(corpseTwo.Id));
                Assert.That(persistence.SaveCallCount, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task StartAsync_WhenNonCorpseContainerExists_ShouldLeaveItUntouched()
    {
        var container = CreateItem((Serial)0x40001050u, CorpsePropertyKeys.ItemId, false);
        var child = CreateItem((Serial)0x40001051u, 0x0AC1, false, container.Id);
        container.AddItem(child, Point2D.Zero);

        var persistence = CreatePersistenceService(container, child);
        var service = new CorpseStartupCleanupService(persistence);

        await service.StartAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Is.Empty);
                Assert.That(persistence.SaveCallCount, Is.EqualTo(0));
                Assert.That(persistence.TestUnitOfWork.Items.Contains(container.Id), Is.True);
                Assert.That(persistence.TestUnitOfWork.Items.Contains(child.Id), Is.True);
            }
        );
    }

    [Test]
    public async Task StartAsync_WhenNoPersistedCorpsesExist_ShouldNotRemoveAnythingOrSave()
    {
        var persistence = CreatePersistenceService(
            CreateItem(
                (Serial)0x40001001u,
                0x0ABC,
                false
            )
        );
        var service = new CorpseStartupCleanupService(persistence);

        await service.StartAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(persistence.SaveCallCount, Is.EqualTo(0));
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Is.Empty);
            }
        );
    }

    [Test]
    public async Task StartAsync_WhenPersistedCorpseExists_ShouldDeleteCorpseAndSave()
    {
        var corpse = CreateCorpse((Serial)0x40001010u);
        var persistence = CreatePersistenceService(corpse);
        var service = new CorpseStartupCleanupService(persistence);

        await service.StartAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Is.EqualTo([corpse.Id]));
                Assert.That(persistence.SaveCallCount, Is.EqualTo(1));
                Assert.That(persistence.TestUnitOfWork.Items.Contains(corpse.Id), Is.False);
            }
        );
    }

    [Test]
    public async Task StartAsync_WhenRemovalFails_ShouldThrowAndNotSave()
    {
        var corpse = CreateCorpse((Serial)0x40001060u);
        var child = CreateItem((Serial)0x40001061u, 0x0AC2, false, corpse.Id);
        corpse.AddItem(child, Point2D.Zero);

        var persistence = CreatePersistenceService(corpse, child);
        persistence.TestUnitOfWork.Items.FailRemove(child.Id);
        var service = new CorpseStartupCleanupService(persistence);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await service.StartAsync());

        Assert.Multiple(
            () =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Message, Does.Contain(child.Id.ToString()));
                Assert.That(persistence.SaveCallCount, Is.EqualTo(0));
                Assert.That(persistence.TestUnitOfWork.Items.Contains(corpse.Id), Is.True);
                Assert.That(persistence.TestUnitOfWork.Items.Contains(child.Id), Is.True);
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Is.EqualTo([child.Id]));
            }
        );
    }

    [Test]
    public async Task StopAsync_ShouldCompleteWithoutSideEffects()
    {
        var persistence = CreatePersistenceService();
        var service = new CorpseStartupCleanupService(persistence);

        await service.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(persistence.SaveCallCount, Is.EqualTo(0));
                Assert.That(persistence.TestUnitOfWork.Items.RemoveCalls, Is.Empty);
            }
        );
    }

    private static UOItemEntity CreateCorpse(Serial id)
    {
        var corpse = CreateItem(id, CorpsePropertyKeys.ItemId, true);
        corpse.Name = "a corpse";

        return corpse;
    }

    private static UOItemEntity CreateItem(Serial id, int itemId, bool isCorpse, Serial parentContainerId = default)
    {
        var item = new UOItemEntity
        {
            Id = id,
            ItemId = itemId,
            ParentContainerId = parentContainerId
        };

        if (isCorpse)
        {
            item.SetCustomBoolean(CorpsePropertyKeys.IsCorpse, true);
        }

        return item;
    }

    private static TestPersistenceService CreatePersistenceService(params UOItemEntity[] items)
    {
        var repository = new TestItemRepository();
        repository.Seed(items);

        return new(repository, new());
    }

    private static TestPersistenceService CreatePersistenceService(
        IReadOnlyCollection<UOItemEntity> items,
        IReadOnlyCollection<UOMobileEntity> mobiles
    )
    {
        var itemRepository = new TestItemRepository();
        itemRepository.Seed(items);

        var mobileRepository = new TestMobileRepository();
        mobileRepository.Seed(mobiles);

        return new(itemRepository, mobileRepository);
    }
}
