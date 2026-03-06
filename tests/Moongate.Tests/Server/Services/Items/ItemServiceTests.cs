using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Items;

public class ItemServiceTests
{
    private sealed class ItemServiceTestsItemFactoryService : IItemFactoryService
    {
        public string? LastTemplateId { get; private set; }

        public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
        {
            LastTemplateId = itemTemplateId;

            return new()
            {
                Id = (Serial)0x40000077u,
                Name = "Template Item",
                ItemId = 0x1F9E,
                Amount = 1,
                Location = Point3D.Zero
            };
        }

        public UOItemEntity GetNewBackpack()
            => throw new NotSupportedException();

        public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? template)
        {
            template = null;

            return false;
        }
    }

    [Test]
    public async Task Clone_ShouldGenerateNewSerial_ByDefault()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());
        var item = new UOItemEntity
        {
            Id = persistence.UnitOfWork.AllocateNextItemId(),
            ItemId = 0x0EED,
            Amount = 42,
            Name = "gold",
            Hue = 3,
            MapId = 2,
            ScriptId = "item.gold"
        };

        var clone = service.Clone(item);

        Assert.Multiple(
            () =>
            {
                Assert.That(clone.Id, Is.Not.EqualTo(item.Id));
                Assert.That(clone.ItemId, Is.EqualTo(item.ItemId));
                Assert.That(clone.Amount, Is.EqualTo(item.Amount));
                Assert.That(clone.Name, Is.EqualTo(item.Name));
                Assert.That(clone.Hue, Is.EqualTo(item.Hue));
                Assert.That(clone.MapId, Is.EqualTo(item.MapId));
                Assert.That(clone.ScriptId, Is.EqualTo(item.ScriptId));
            }
        );
    }

    [Test]
    public async Task Clone_ShouldKeepSerial_WhenGenerateNewSerialIsFalse()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());
        var originalId = persistence.UnitOfWork.AllocateNextItemId();
        var item = new UOItemEntity
        {
            Id = originalId,
            ItemId = 0x0E75
        };

        var clone = service.Clone(item, false);

        Assert.That(clone.Id, Is.EqualTo(originalId));
    }

    [Test]
    public async Task CloneAsync_ShouldClonePersistedItem()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());
        var itemId = persistence.UnitOfWork.AllocateNextItemId();

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = itemId,
                ItemId = 0x13B2,
                Amount = 1,
                Name = "sword",
                Weight = 6
            }
        );

        var clone = await service.CloneAsync(itemId);

        Assert.Multiple(
            () =>
            {
                Assert.That(clone, Is.Not.Null);
                Assert.That(clone!.Id, Is.Not.EqualTo(itemId));
                Assert.That(clone.ItemId, Is.EqualTo(0x13B2));
                Assert.That(clone.Name, Is.EqualTo("sword"));
                Assert.That(clone.Weight, Is.EqualTo(6));
            }
        );
    }

    [Test]
    public async Task CloneAsync_ShouldReturnNull_WhenItemDoesNotExist()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());

        var clone = await service.CloneAsync((Serial)0x4000FFFFu);

        Assert.That(clone, Is.Null);
    }

    [Test]
    public async Task CreateItemAsync_ShouldAllocateIdAndPersist()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());
        var item = new UOItemEntity
        {
            ItemId = 0x0EED,
            Amount = 25
        };

        var itemId = await service.CreateItemAsync(item);
        var saved = await persistence.UnitOfWork.Items.GetByIdAsync(itemId);

        Assert.Multiple(
            () =>
            {
                Assert.That(itemId, Is.Not.EqualTo(Serial.Zero));
                Assert.That(saved, Is.Not.Null);
                Assert.That(saved!.ItemId, Is.EqualTo(0x0EED));
                Assert.That(saved.Amount, Is.EqualTo(25));
            }
        );
    }

    [Test]
    public async Task DeleteItemAsync_ShouldRemoveItem()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());
        var itemId = persistence.UnitOfWork.AllocateNextItemId();

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = itemId,
                ItemId = 0x0EED
            }
        );

        var deleted = await service.DeleteItemAsync(itemId);
        var reloaded = await persistence.UnitOfWork.Items.GetByIdAsync(itemId);

        Assert.Multiple(
            () =>
            {
                Assert.That(deleted, Is.True);
                Assert.That(reloaded, Is.Null);
            }
        );
    }

    [Test]
    public async Task DropItemToGroundAsync_ShouldReturnDropContext_AndDetachFromContainer()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());
        var containerId = persistence.UnitOfWork.AllocateNextItemId();
        var itemId = persistence.UnitOfWork.AllocateNextItemId();
        var oldLocation = new Point3D(12, 34, 0);
        var newLocation = new Point3D(100, 200, 7);

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = containerId,
                ItemId = 0x0E75
            }
        );

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = itemId,
                ItemId = 0x0EED,
                ParentContainerId = containerId,
                Location = oldLocation
            }
        );

        var result = await service.DropItemToGroundAsync(itemId, newLocation, 5);
        var reloadedItem = await persistence.UnitOfWork.Items.GetByIdAsync(itemId);
        var reloadedContainer = await persistence.UnitOfWork.Items.GetByIdAsync(containerId);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Value.ItemId, Is.EqualTo(itemId));
                Assert.That(result.Value.SourceContainerId, Is.EqualTo(containerId));
                Assert.That(result.Value.OldLocation, Is.EqualTo(oldLocation));
                Assert.That(result.Value.NewLocation, Is.EqualTo(newLocation));
                Assert.That(reloadedItem, Is.Not.Null);
                Assert.That(reloadedItem!.ParentContainerId, Is.EqualTo(Serial.Zero));
                Assert.That(reloadedItem.Location, Is.EqualTo(newLocation));
                Assert.That(reloadedItem.MapId, Is.EqualTo(5));
                Assert.That(reloadedContainer, Is.Not.Null);
                Assert.That(reloadedContainer!.ContainedItemIds.Contains(itemId), Is.False);
            }
        );
    }

    [Test]
    public async Task EquipItemAsync_ShouldSetItemEquipmentAndMobileLayer()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        IItemService service = new ItemService(persistence, gameEventBus);
        var mobileId = persistence.UnitOfWork.AllocateNextMobileId();
        var itemId = persistence.UnitOfWork.AllocateNextItemId();

        await persistence.UnitOfWork.Mobiles.UpsertAsync(
            new()
            {
                Id = mobileId,
                Name = "equip-target",
                MapId = 3
            }
        );

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = itemId,
                ItemId = 0x1517,
                ParentContainerId = persistence.UnitOfWork.AllocateNextItemId(),
                ContainerPosition = new(3, 3)
            }
        );

        var equipped = await service.EquipItemAsync(itemId, mobileId, ItemLayerType.Shirt);
        var reloadedItem = await persistence.UnitOfWork.Items.GetByIdAsync(itemId);
        var reloadedMobile = await persistence.UnitOfWork.Mobiles.GetByIdAsync(mobileId);
        var equippedEvent = gameEventBus.Events.OfType<ItemEquippedEvent>().LastOrDefault();

        Assert.Multiple(
            () =>
            {
                Assert.That(equipped, Is.True);
                Assert.That(reloadedItem, Is.Not.Null);
                Assert.That(reloadedItem!.EquippedMobileId, Is.EqualTo(mobileId));
                Assert.That(reloadedItem.EquippedLayer, Is.EqualTo(ItemLayerType.Shirt));
                Assert.That(reloadedItem.MapId, Is.EqualTo(3));
                Assert.That(reloadedItem.ParentContainerId, Is.EqualTo(Serial.Zero));
                Assert.That(reloadedMobile, Is.Not.Null);
                Assert.That(reloadedMobile!.EquippedItemIds[ItemLayerType.Shirt], Is.EqualTo(itemId));
                Assert.That(equippedEvent.ItemId, Is.EqualTo(itemId));
                Assert.That(equippedEvent.MobileId, Is.EqualTo(mobileId));
                Assert.That(equippedEvent.Layer, Is.EqualTo(ItemLayerType.Shirt));
                Assert.That(equippedEvent.BaseEvent.Timestamp, Is.GreaterThan(0));
            }
        );
    }

    [Test]
    public async Task GetGroundItemsInSectorAsync_ShouldFilterByMapAndGroundState()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = persistence.UnitOfWork.AllocateNextItemId(),
                ItemId = 0x0EED,
                MapId = 1,
                Location = new(130, 130, 0)
            }
        );
        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = persistence.UnitOfWork.AllocateNextItemId(),
                ItemId = 0x0EED,
                MapId = 2,
                Location = new(130, 130, 0)
            }
        );
        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = persistence.UnitOfWork.AllocateNextItemId(),
                ItemId = 0x0EED,
                MapId = 1,
                Location = new(130, 130, 0),
                ParentContainerId = (Serial)0x40000001u
            }
        );

        var items = await service.GetGroundItemsInSectorAsync(1, 4, 4);

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].MapId, Is.EqualTo(1));
        Assert.That(items[0].ParentContainerId, Is.EqualTo(Serial.Zero));
    }

    [Test]
    public async Task GetItemAsync_ShouldHydrateContainedItemsAndReferences()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());
        var containerId = persistence.UnitOfWork.AllocateNextItemId();
        var childId = persistence.UnitOfWork.AllocateNextItemId();

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = containerId,
                ItemId = 0x0E75
            }
        );
        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = childId,
                ItemId = 0x0EED,
                ParentContainerId = containerId,
                ContainerPosition = new(7, 9),
                Amount = 88
            }
        );

        var container = await service.GetItemAsync(containerId);

        Assert.Multiple(
            () =>
            {
                Assert.That(container, Is.Not.Null);
                Assert.That(container!.Items.Count, Is.EqualTo(1));
                Assert.That(container.Items[0].Id, Is.EqualTo(childId));
                Assert.That(container.Items[0].Location, Is.EqualTo(new Point3D(7, 9, 0)));
                Assert.That(container.ContainedItemIds, Has.Count.EqualTo(1));
                Assert.That(container.ContainedItemIds[0], Is.EqualTo(childId));
                Assert.That(container.ContainedItemReferences.ContainsKey(childId), Is.True);
            }
        );
    }

    [Test]
    public async Task GetItemAsync_ShouldHydrateNestedContainersRecursively()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());
        var rootContainerId = persistence.UnitOfWork.AllocateNextItemId();
        var nestedContainerId = persistence.UnitOfWork.AllocateNextItemId();
        var nestedItemId = persistence.UnitOfWork.AllocateNextItemId();

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = rootContainerId,
                ItemId = 0x0E75
            }
        );
        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = nestedContainerId,
                ItemId = 0x09A8,
                ParentContainerId = rootContainerId,
                ContainerPosition = new(3, 4)
            }
        );
        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = nestedItemId,
                ItemId = 0x0EED,
                ParentContainerId = nestedContainerId,
                ContainerPosition = new(8, 9),
                Amount = 10
            }
        );

        var root = await service.GetItemAsync(rootContainerId);

        Assert.Multiple(
            () =>
            {
                Assert.That(root, Is.Not.Null);
                Assert.That(root!.Items.Count, Is.EqualTo(1));
                Assert.That(root.Items[0].Id, Is.EqualTo(nestedContainerId));
                Assert.That(root.Items[0].Location, Is.EqualTo(new Point3D(3, 4, 0)));

                var nested = root.Items[0];
                Assert.That(nested.Items.Count, Is.EqualTo(1));
                Assert.That(nested.Items[0].Id, Is.EqualTo(nestedItemId));
                Assert.That(nested.Items[0].Location, Is.EqualTo(new Point3D(8, 9, 0)));
                Assert.That(nested.ContainedItemReferences.ContainsKey(nestedItemId), Is.True);
            }
        );
    }

    [Test]
    public async Task GetItemsInContainerAsync_ShouldReturnOnlyMatchingItems()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());
        var containerId = persistence.UnitOfWork.AllocateNextItemId();
        var containedItemId = persistence.UnitOfWork.AllocateNextItemId();

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = containerId,
                ItemId = 0x0E75
            }
        );
        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = containedItemId,
                ItemId = 0x0EED,
                ParentContainerId = containerId
            }
        );
        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = persistence.UnitOfWork.AllocateNextItemId(),
                ItemId = 0x0E21
            }
        );

        var items = await service.GetItemsInContainerAsync(containerId);

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].Id, Is.EqualTo(containedItemId));
    }

    [Test]
    public async Task MoveItemToContainerAsync_ShouldPreserveExistingContainedItems_WhenContainerReferencesAreNotHydrated()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());
        var backpackId = persistence.UnitOfWork.AllocateNextItemId();
        var goldId = persistence.UnitOfWork.AllocateNextItemId();
        var pantsId = persistence.UnitOfWork.AllocateNextItemId();

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = backpackId,
                ItemId = 0x0E75,
                ContainedItemIds = []
            }
        );

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = goldId,
                ItemId = 0x0EED,
                Amount = 100,
                ParentContainerId = backpackId,
                ContainerPosition = new(20, 20)
            }
        );

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = pantsId,
                ItemId = 0x152E,
                ParentContainerId = Serial.Zero
            }
        );

        var moved = await service.MoveItemToContainerAsync(pantsId, backpackId, new(45, 55));
        var hydratedBackpack = await service.GetItemAsync(backpackId);

        Assert.Multiple(
            () =>
            {
                Assert.That(moved, Is.True);
                Assert.That(hydratedBackpack, Is.Not.Null);
                Assert.That(hydratedBackpack!.Items.Select(item => item.Id), Contains.Item(goldId));
                Assert.That(hydratedBackpack.Items.Select(item => item.Id), Contains.Item(pantsId));
            }
        );
    }

    [Test]
    public async Task MoveItemToContainerAsync_ShouldSetContainerFields_AndClearEquipState()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        IItemService service = new ItemService(persistence, gameEventBus);
        var mobileId = persistence.UnitOfWork.AllocateNextMobileId();
        var itemId = persistence.UnitOfWork.AllocateNextItemId();
        var containerId = persistence.UnitOfWork.AllocateNextItemId();

        await persistence.UnitOfWork.Mobiles.UpsertAsync(
            new()
            {
                Id = mobileId,
                Name = "item-owner",
                EquippedItemIds =
                {
                    [ItemLayerType.OneHanded] = itemId
                }
            }
        );

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = itemId,
                ItemId = 0x13B2,
                EquippedMobileId = mobileId,
                EquippedLayer = ItemLayerType.OneHanded
            }
        );

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = containerId,
                ItemId = 0x0E75
            }
        );

        var moved = await service.MoveItemToContainerAsync(itemId, containerId, new(55, 77), 4242);
        var reloadedItem = await persistence.UnitOfWork.Items.GetByIdAsync(itemId);
        var reloadedMobile = await persistence.UnitOfWork.Mobiles.GetByIdAsync(mobileId);
        var movedEvent = gameEventBus.Events.OfType<ItemMovedEvent>().LastOrDefault();

        Assert.Multiple(
            () =>
            {
                Assert.That(moved, Is.True);
                Assert.That(reloadedItem, Is.Not.Null);
                Assert.That(reloadedItem!.ParentContainerId, Is.EqualTo(containerId));
                Assert.That(reloadedItem.ContainerPosition, Is.EqualTo(new Point2D(55, 77)));
                Assert.That(reloadedItem.EquippedMobileId, Is.EqualTo(Serial.Zero));
                Assert.That(reloadedItem.EquippedLayer, Is.Null);
                Assert.That(reloadedMobile, Is.Not.Null);
                Assert.That(reloadedMobile!.EquippedItemIds.ContainsKey(ItemLayerType.OneHanded), Is.False);
                Assert.That(movedEvent.ItemId, Is.EqualTo(itemId));
                Assert.That(movedEvent.SessionId, Is.EqualTo(4242));
                Assert.That(movedEvent.OldContainerId, Is.EqualTo(Serial.Zero));
                Assert.That(movedEvent.NewContainerId, Is.EqualTo(containerId));
                Assert.That(movedEvent.MapId, Is.EqualTo(reloadedItem.MapId));
            }
        );
    }

    [Test]
    public async Task MoveItemToWorldAsync_ShouldDetachAndSetLocation()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        IItemService service = new ItemService(persistence, gameEventBus);
        var mobileId = persistence.UnitOfWork.AllocateNextMobileId();
        var itemId = persistence.UnitOfWork.AllocateNextItemId();

        await persistence.UnitOfWork.Mobiles.UpsertAsync(
            new()
            {
                Id = mobileId,
                Name = "world-drop-owner",
                EquippedItemIds =
                {
                    [ItemLayerType.OuterTorso] = itemId
                }
            }
        );

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = itemId,
                ItemId = 0x1F03,
                EquippedMobileId = mobileId,
                EquippedLayer = ItemLayerType.OuterTorso
            }
        );

        var moved = await service.MoveItemToWorldAsync(itemId, new(1111, 2222, 7), 4, 9001);
        var reloadedItem = await persistence.UnitOfWork.Items.GetByIdAsync(itemId);
        var reloadedMobile = await persistence.UnitOfWork.Mobiles.GetByIdAsync(mobileId);
        var movedEvent = gameEventBus.Events.OfType<ItemMovedEvent>().LastOrDefault();

        Assert.Multiple(
            () =>
            {
                Assert.That(moved, Is.True);
                Assert.That(reloadedItem, Is.Not.Null);
                Assert.That(reloadedItem!.Location, Is.EqualTo(new Point3D(1111, 2222, 7)));
                Assert.That(reloadedItem.MapId, Is.EqualTo(4));
                Assert.That(reloadedItem.EquippedMobileId, Is.EqualTo(Serial.Zero));
                Assert.That(reloadedItem.EquippedLayer, Is.Null);
                Assert.That(reloadedMobile, Is.Not.Null);
                Assert.That(reloadedMobile!.EquippedItemIds.ContainsKey(ItemLayerType.OuterTorso), Is.False);
                Assert.That(movedEvent.ItemId, Is.EqualTo(itemId));
                Assert.That(movedEvent.SessionId, Is.EqualTo(9001));
                Assert.That(movedEvent.OldContainerId, Is.EqualTo(Serial.Zero));
                Assert.That(movedEvent.NewContainerId, Is.EqualTo(Serial.Zero));
                Assert.That(movedEvent.MapId, Is.EqualTo(4));
            }
        );
    }

    [Test]
    public async Task SpawnFromTemplateAsync_ShouldCreateAndPersistItemFromTemplate()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        var itemFactory = new ItemServiceTestsItemFactoryService();
        IItemService service = new ItemService(persistence, gameEventBus, itemFactory);

        var item = await service.SpawnFromTemplateAsync("brick");
        var persisted = await persistence.UnitOfWork.Items.GetByIdAsync(item.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(itemFactory.LastTemplateId, Is.EqualTo("brick"));
                Assert.That(item.Id, Is.EqualTo((Serial)0x40000077u));
                Assert.That(persisted, Is.Not.Null);
                Assert.That(persisted!.Name, Is.EqualTo("Template Item"));
                Assert.That(persisted.ItemId, Is.EqualTo(0x1F9E));
            }
        );
    }

    [Test]
    public async Task TryToGetItemAsync_ShouldReturnFoundAndItem_WhenItemExists()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());
        var itemId = persistence.UnitOfWork.AllocateNextItemId();

        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = itemId,
                ItemId = 0x0EED,
                Amount = 100
            }
        );

        var result = await service.TryToGetItemAsync(itemId);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Found, Is.True);
                Assert.That(result.Item, Is.Not.Null);
                Assert.That(result.Item!.Id, Is.EqualTo(itemId));
            }
        );
    }

    [Test]
    public async Task TryToGetItemAsync_ShouldReturnNotFoundAndNull_WhenItemMissing()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());

        var result = await service.TryToGetItemAsync((Serial)0x4000FFFFu);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Found, Is.False);
                Assert.That(result.Item, Is.Null);
            }
        );
    }

    [Test]
    public async Task UpsertItemsAsync_ShouldPersistAllItems_AndAllocateMissingSerials()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        IItemService service = new ItemService(persistence, new NetworkServiceTestGameEventBusService());
        var fixedId = persistence.UnitOfWork.AllocateNextItemId();
        var first = new UOItemEntity
        {
            Id = Serial.Zero,
            ItemId = 0x0EED,
            Amount = 100
        };
        var second = new UOItemEntity
        {
            Id = fixedId,
            ItemId = 0x0E75
        };

        await service.UpsertItemsAsync(first, second);

        var all = await persistence.UnitOfWork.Items.GetAllAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(first.Id, Is.Not.EqualTo(Serial.Zero));
                Assert.That(all.Count, Is.EqualTo(2));
                Assert.That(all.Any(item => item.Id == first.Id && item.ItemId == 0x0EED), Is.True);
                Assert.That(all.Any(item => item.Id == fixedId && item.ItemId == 0x0E75), Is.True);
            }
        );
    }

    private static async Task<PersistenceService> CreatePersistenceServiceAsync(string rootDirectory)
    {
        var directories = new DirectoriesConfig(rootDirectory, Enum.GetNames<DirectoryType>());
        var persistence = new PersistenceService(
            directories,
            new TimerWheelService(
                new()
                {
                    TickDuration = TimeSpan.FromMilliseconds(250),
                    WheelSize = 512
                }
            ),
            new(),
            new NetworkServiceTestGameEventBusService()
        );
        await persistence.StartAsync();

        return persistence;
    }
}
