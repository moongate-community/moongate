using System.Globalization;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Services.Items;
using Moongate.Server.Types.Items;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Loot;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Items;

public sealed class LootGenerationServiceTests
{
    private sealed class TestItemFactoryService : IItemFactoryService
    {
        private uint _nextSerial = 0x40000100;

        public Dictionary<string, ItemTemplateDefinition> Templates { get; } = new(StringComparer.OrdinalIgnoreCase);

        public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
        {
            if (string.Equals(itemTemplateId, "gold", StringComparison.OrdinalIgnoreCase))
            {
                return new()
                {
                    Id = (Serial)_nextSerial++,
                    ItemId = 0x0EED,
                    Name = "gold",
                    Amount = 1,
                    IsStackable = true
                };
            }

            if (Templates.TryGetValue(itemTemplateId, out var template))
            {
                return new()
                {
                    Id = (Serial)_nextSerial++,
                    ItemId = ParseItemId(template.ItemId),
                    Name = template.Id,
                    Amount = 1,
                    IsStackable = false
                };
            }

            throw new NotSupportedException($"Unsupported template {itemTemplateId}");
        }

        public UOItemEntity GetNewBackpack()
            => throw new NotSupportedException();

        public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? template)
            => Templates.TryGetValue(itemTemplateId, out template);

        private static int ParseItemId(string itemId)
        {
            if (itemId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.Parse(itemId.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return int.Parse(itemId, CultureInfo.InvariantCulture);
        }
    }

    private sealed class TestItemService : IItemService
    {
        public Dictionary<Serial, UOItemEntity> ItemsById { get; } = [];

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => throw new NotSupportedException();

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
        {
            ItemsById[item.Id] = item;

            return Task.FromResult(item.Id);
        }

        public Task<bool> DeleteItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(ItemsById.TryGetValue(itemId, out var item) ? item : null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(
            Serial itemId,
            Serial containerId,
            Point2D position,
            long sessionId = 0
        )
        {
            var item = ItemsById[itemId];
            var container = ItemsById[containerId];
            container.AddItem(item, position);
            ItemsById[containerId] = container;
            ItemsById[itemId] = item;

            return Task.FromResult(true);
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task UpsertItemAsync(UOItemEntity item)
        {
            ItemsById[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => throw new NotSupportedException();
    }

    private sealed class TestLootTemplateService : ILootTemplateService
    {
        public Dictionary<string, LootTemplateDefinition> Templates { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int Count => Templates.Count;

        public void Clear()
            => Templates.Clear();

        public IReadOnlyList<LootTemplateDefinition> GetAll()
            => Templates.Values.ToList();

        public bool TryGet(string templateId, out LootTemplateDefinition? template)
            => Templates.TryGetValue(templateId, out template);

        public void Upsert(LootTemplateDefinition definition)
            => Templates[definition.Id] = definition;

        public void UpsertRange(IEnumerable<LootTemplateDefinition> templates)
        {
            foreach (var template in templates)
            {
                Templates[template.Id] = template;
            }
        }
    }

    private sealed class TestItemTemplateService : IItemTemplateService
    {
        public Dictionary<string, ItemTemplateDefinition> Templates { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int Count => Templates.Count;

        public void Clear()
            => Templates.Clear();

        public IReadOnlyList<ItemTemplateDefinition> GetAll()
            => Templates.Values.ToList();

        public bool TryGet(string id, out ItemTemplateDefinition? definition)
            => Templates.TryGetValue(id, out definition);

        public void Upsert(ItemTemplateDefinition definition)
            => Templates[definition.Id] = definition;

        public void UpsertRange(IEnumerable<ItemTemplateDefinition> templates)
        {
            foreach (var template in templates)
            {
                Templates[template.Id] = template;
            }
        }
    }

    [Test]
    public async Task EnsureLootGeneratedAsync_WhenRefillWindowHasNotElapsed_ShouldNotRefillEmptyContainer()
    {
        TileData.ItemTable[0x0E75] = new(string.Empty, UOTileFlag.Container, 0, 0, 0, 0, 0, 0);
        var itemFactory = CreateItemFactory();
        var itemService = new TestItemService();
        var lootTemplateService = CreateLootTemplateService();
        var itemTemplateService = CreateItemTemplateService();
        var service = new LootGenerationService(itemFactory, itemService, lootTemplateService, itemTemplateService);
        var container = CreateRefillableContainer(DateTime.UtcNow.AddMinutes(5));
        itemService.ItemsById[container.Id] = container;

        var result = await service.EnsureLootGeneratedAsync(container);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Items, Is.Empty);
                Assert.That(itemService.ItemsById, Has.Count.EqualTo(1));
                Assert.That(
                    result.TryGetCustomString(ItemCustomParamKeys.Loot.RefillReadyAtUtc, out var refillReadyRaw),
                    Is.True
                );
                Assert.That(refillReadyRaw, Is.Not.Null.And.Not.Empty);
            }
        );
    }

    [Test]
    public async Task EnsureLootGeneratedAsync_WhenRefillWindowHasElapsed_ShouldRefillEmptyContainer()
    {
        TileData.ItemTable[0x0E75] = new(string.Empty, UOTileFlag.Container, 0, 0, 0, 0, 0, 0);
        var itemFactory = CreateItemFactory();
        var itemService = new TestItemService();
        var lootTemplateService = CreateLootTemplateService();
        var itemTemplateService = CreateItemTemplateService();
        var service = new LootGenerationService(itemFactory, itemService, lootTemplateService, itemTemplateService);
        var container = CreateRefillableContainer(DateTime.UtcNow.AddMinutes(-5));
        itemService.ItemsById[container.Id] = container;

        var result = await service.EnsureLootGeneratedAsync(container);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Items, Has.Count.EqualTo(1));
                Assert.That(result.Items[0].Name, Is.EqualTo("gold"));
                Assert.That(result.Items[0].Amount, Is.EqualTo(125));
                Assert.That(result.TryGetCustomBoolean(ItemCustomParamKeys.Loot.Generated, out var generated), Is.True);
                Assert.That(generated, Is.True);
                Assert.That(
                    result.TryGetCustomString(ItemCustomParamKeys.Loot.RefillReadyAtUtc, out _),
                    Is.False
                );
            }
        );
    }

    [Test]
    public async Task EnsureLootGeneratedAsync_WhenLootEntryUsesItemTag_ShouldCreateRandomMatchingTemplateItem()
    {
        TileData.ItemTable[0x0E75] = new(string.Empty, UOTileFlag.Container, 0, 0, 0, 0, 0, 0);
        var itemFactory = CreateItemFactory();
        var itemService = new TestItemService();
        var lootTemplateService = CreateLootTemplateService();
        var itemTemplateService = CreateItemTemplateService();
        lootTemplateService.Templates["loot_test_chest_basic"].Entries =
        [
            new LootTemplateEntry
            {
                ItemTag = "weapon.ranged",
                Weight = 1,
                Amount = 1
            }
        ];

        var service = new LootGenerationService(itemFactory, itemService, lootTemplateService, itemTemplateService);
        var container = CreateRefillableContainer(DateTime.UtcNow.AddMinutes(-5));
        itemService.ItemsById[container.Id] = container;

        var result = await service.EnsureLootGeneratedAsync(container);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Name, Is.EqualTo("bow"));
    }

    [Test]
    public async Task EnsureLootGeneratedAsync_WhenLootTableDefinesMultipleRolls_ShouldCreateItemsForEachRoll()
    {
        TileData.ItemTable[0x0E75] = new(string.Empty, UOTileFlag.Container, 0, 0, 0, 0, 0, 0);
        var itemFactory = CreateItemFactory();
        var itemService = new TestItemService();
        var lootTemplateService = CreateLootTemplateService();
        var itemTemplateService = CreateItemTemplateService();
        lootTemplateService.Templates["loot_test_chest_basic"].Rolls = 3;

        var service = new LootGenerationService(itemFactory, itemService, lootTemplateService, itemTemplateService);
        var container = CreateRefillableContainer(DateTime.UtcNow.AddMinutes(-5));
        itemService.ItemsById[container.Id] = container;

        var result = await service.EnsureLootGeneratedAsync(container);

        Assert.That(result.Items, Has.Count.EqualTo(3));
        Assert.That(result.Items.All(static item => item.Name == "gold"), Is.True);
        Assert.That(result.Items.Select(static item => item.Amount), Is.All.EqualTo(125));
    }

    [Test]
    public async Task GenerateForContainerAsync_WhenModeIsOnDeath_ShouldBypassFirstOpenLootFlags()
    {
        TileData.ItemTable[0x2006] = new(string.Empty, UOTileFlag.Container, 0, 0, 0, 0, 0, 0);
        var itemFactory = CreateItemFactory();
        var itemService = new TestItemService();
        var lootTemplateService = CreateLootTemplateService();
        var itemTemplateService = CreateItemTemplateService();
        var service = new LootGenerationService(itemFactory, itemService, lootTemplateService, itemTemplateService);
        var corpse = new UOItemEntity
        {
            Id = (Serial)0x40000050u,
            ItemId = 0x2006,
            MapId = 1,
            Name = "a corpse"
        };
        corpse.SetCustomBoolean(ItemCustomParamKeys.Loot.Generated, true);
        itemService.ItemsById[corpse.Id] = corpse;

        var result = await service.GenerateForContainerAsync(
            corpse,
            ["loot_test_chest_basic"],
            LootGenerationMode.OnDeath
        );

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Name, Is.EqualTo("gold"));
    }

    private static TestItemFactoryService CreateItemFactory()
    {
        var itemFactory = new TestItemFactoryService();
        itemFactory.Templates["loot_test_chest"] = new()
        {
            Id = "loot_test_chest",
            Description = "Refillable loot test chest",
            GoldValue = GoldValueSpec.FromValue(0),
            ItemId = "0x0E75",
            ScriptId = "none",
            LootTables = ["loot_test_chest_basic"],
            Params = new(StringComparer.OrdinalIgnoreCase)
            {
                ["loot_refillable"] = new() { Type = ItemTemplateParamType.String, Value = "true" },
                ["loot_refill_seconds"] = new() { Type = ItemTemplateParamType.String, Value = "300" }
            }
        };
        itemFactory.Templates["bow"] = new()
        {
            Id = "bow",
            Description = "Bow",
            GoldValue = GoldValueSpec.FromValue(0),
            ItemId = "0x13B2",
            ScriptId = "none",
            Tags = ["weapon.ranged"]
        };

        return itemFactory;
    }

    private static TestLootTemplateService CreateLootTemplateService()
    {
        var lootTemplateService = new TestLootTemplateService();
        lootTemplateService.Templates["loot_test_chest_basic"] = new()
        {
            Id = "loot_test_chest_basic",
            Name = "Loot Test Chest Basic",
            Category = "Test",
            Description = "Single-entry deterministic loot table for tests",
            Entries =
            [
                new LootTemplateEntry
                {
                    ItemTemplateId = "gold",
                    Weight = 1,
                    Amount = 125
                }
            ]
        };

        return lootTemplateService;
    }

    private static TestItemTemplateService CreateItemTemplateService()
    {
        var itemTemplateService = new TestItemTemplateService();
        itemTemplateService.Templates["bow"] = new()
        {
            Id = "bow",
            Description = "Bow",
            GoldValue = GoldValueSpec.FromValue(0),
            ItemId = "0x13B2",
            ScriptId = "none",
            Tags = ["weapon.ranged"]
        };

        return itemTemplateService;
    }

    private static UOItemEntity CreateRefillableContainer(DateTime refillReadyAtUtc)
    {
        var container = new UOItemEntity
        {
            Id = (Serial)0x40000001u,
            ItemId = 0x0E75
        };
        container.SetCustomString(ItemCustomParamKeys.Item.TemplateId, "loot_test_chest");
        container.SetCustomBoolean(ItemCustomParamKeys.Loot.Generated, true);
        container.SetCustomString(
            ItemCustomParamKeys.Loot.RefillReadyAtUtc,
            refillReadyAtUtc.ToString("O", CultureInfo.InvariantCulture)
        );

        return container;
    }
}
