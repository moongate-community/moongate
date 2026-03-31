using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Quests;
using Moongate.Server.Services.Quests;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Quests;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Quests;

[TestFixture]
public sealed class QuestServiceTests
{
    private QuestTemplateService _questTemplateService = null!;
    private FakeMobileService _mobileService = null!;
    private FakeCharacterService _characterService = null!;
    private FakeItemService _itemService = null!;
    private FakeItemFactoryService _itemFactoryService = null!;
    private IQuestService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _questTemplateService = new QuestTemplateService();
        _mobileService = new FakeMobileService();
        _characterService = new FakeCharacterService();
        _itemService = new FakeItemService();
        _itemFactoryService = new FakeItemFactoryService();
        _service = new QuestService(
            _questTemplateService,
            _mobileService,
            _characterService,
            _itemService,
            _itemFactoryService
        );
    }

    [Test]
    public async Task AcceptAsync_WhenQuestMatchesNpc_ShouldPersistActiveProgress()
    {
        var player = CreatePlayer();
        var npc = CreateMobile((Serial)0x00002001u, "farmer_npc");
        _questTemplateService.Upsert(CreateKillQuest("starter.rat_hunt"));

        var accepted = await _service.AcceptAsync(player, npc, "starter.rat_hunt");

        Assert.Multiple(() =>
        {
            Assert.That(accepted, Is.True);
            Assert.That(player.QuestProgress, Has.Count.EqualTo(1));
            Assert.That(player.QuestProgress[0].QuestId, Is.EqualTo("starter.rat_hunt"));
            Assert.That(player.QuestProgress[0].Status, Is.EqualTo(QuestProgressStatusType.Active));
            Assert.That(player.QuestProgress[0].Objectives, Has.Count.EqualTo(1));
            Assert.That(player.QuestProgress[0].Objectives[0].ObjectiveIndex, Is.EqualTo(0));
            Assert.That(player.QuestProgress[0].Objectives[0].CurrentAmount, Is.EqualTo(0));
            Assert.That(_mobileService.UpsertedMobiles, Is.EqualTo([player.Id]));
        });
    }

    [Test]
    public async Task GetAvailableForNpcAsync_WhenQuestCanBeAccepted_ShouldReturnMatchingQuest()
    {
        var player = CreatePlayer();
        var npc = CreateMobile((Serial)0x00002001u, "farmer_npc");
        _questTemplateService.Upsert(CreateKillQuest("starter.rat_hunt"));

        var available = await _service.GetAvailableForNpcAsync(player, npc);

        Assert.That(available.Select(static quest => quest.Id), Is.EqualTo(["starter.rat_hunt"]));
    }

    [Test]
    public async Task AcceptAsync_WhenQuestIsAlreadyActive_ShouldBlockDuplicateAcceptance()
    {
        var player = CreatePlayer();
        var npc = CreateMobile((Serial)0x00002001u, "farmer_npc");
        _questTemplateService.Upsert(CreateKillQuest("starter.rat_hunt"));

        _ = await _service.AcceptAsync(player, npc, "starter.rat_hunt");
        var acceptedAgain = await _service.AcceptAsync(player, npc, "starter.rat_hunt");

        Assert.Multiple(() =>
        {
            Assert.That(acceptedAgain, Is.False);
            Assert.That(player.QuestProgress, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task AcceptAsync_WhenAnotherQuestIsActive_ShouldNotTreatMaxActiveAsGlobalJournalCap()
    {
        var player = CreatePlayer();
        var npc = CreateMobile((Serial)0x00002001u, "farmer_npc");
        _questTemplateService.Upsert(CreateKillQuest("starter.rat_hunt"));
        _questTemplateService.Upsert(
            new()
            {
                Id = "starter.apple_delivery",
                Name = "Apple Delivery",
                Category = "starter",
                Description = "Bring apples to the farmer.",
                QuestGiverTemplateIds = ["farmer_npc"],
                CompletionNpcTemplateIds = ["farmer_npc"],
                Repeatable = false,
                MaxActivePerCharacter = 1,
                Objectives =
                [
                    new()
                    {
                        Type = QuestObjectiveType.Deliver,
                        ItemTemplateId = "apple",
                        Amount = 3
                    }
                ]
            }
        );

        var acceptedFirst = await _service.AcceptAsync(player, npc, "starter.rat_hunt");
        var acceptedSecond = await _service.AcceptAsync(player, npc, "starter.apple_delivery");

        Assert.Multiple(() =>
        {
            Assert.That(acceptedFirst, Is.True);
            Assert.That(acceptedSecond, Is.True);
            Assert.That(player.QuestProgress, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task AcceptAsync_WhenQuestIsRepeatableAndPreviouslyCompleted_ShouldAllowAcceptingAgain()
    {
        var player = CreatePlayer();
        var npc = CreateMobile((Serial)0x00002001u, "farmer_npc");
        var backpack = CreateBackpack(CreateItem((Serial)0x40001001u, "apple", 3));
        _characterService.Backpack = backpack;
        _questTemplateService.Upsert(
            new()
            {
                Id = "starter.apple_delivery",
                Name = "Apple Delivery",
                Category = "starter",
                Description = "Bring apples to the farmer.",
                QuestGiverTemplateIds = ["farmer_npc"],
                CompletionNpcTemplateIds = ["farmer_npc"],
                Repeatable = true,
                MaxActivePerCharacter = 10,
                Objectives =
                [
                    new()
                    {
                        Type = QuestObjectiveType.Deliver,
                        ItemTemplateId = "apple",
                        Amount = 3
                    }
                ]
            }
        );

        _ = await _service.AcceptAsync(player, npc, "starter.apple_delivery");
        await _service.ReevaluateInventoryAsync(player);
        var completed = await _service.TryCompleteAsync(player, npc, "starter.apple_delivery");
        var acceptedAgain = await _service.AcceptAsync(player, npc, "starter.apple_delivery");

        Assert.Multiple(() =>
        {
            Assert.That(completed, Is.True);
            Assert.That(acceptedAgain, Is.True);
            Assert.That(player.QuestProgress.Count(static progress => progress.QuestId == "starter.apple_delivery"), Is.EqualTo(2));
            Assert.That(player.QuestProgress.Count(static progress => progress.Status == QuestProgressStatusType.Completed), Is.EqualTo(1));
            Assert.That(player.QuestProgress.Count(static progress => progress.Status == QuestProgressStatusType.Active), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task OnMobileKilledAsync_WhenQuestObjectivesAreReordered_ShouldPreserveProgressByStableObjectiveIdentity()
    {
        var player = CreatePlayer();
        var npc = CreateMobile((Serial)0x00002001u, "farmer_npc");
        var quest = new QuestTemplateDefinition
        {
            Id = "starter.double_hunt",
            Name = "Double Hunt",
            Category = "starter",
            Description = "Kill two different pests.",
            QuestGiverTemplateIds = ["farmer_npc"],
            CompletionNpcTemplateIds = ["farmer_npc"],
            Repeatable = false,
            MaxActivePerCharacter = 1,
            Objectives =
            [
                new()
                {
                    Type = QuestObjectiveType.Kill,
                    MobileTemplateIds = ["sewer_rat"],
                    Amount = 1
                },
                new()
                {
                    Type = QuestObjectiveType.Kill,
                    MobileTemplateIds = ["giant_rat"],
                    Amount = 1
                }
            ]
        };
        _questTemplateService.Upsert(quest);

        _ = await _service.AcceptAsync(player, npc, quest.Id);
        var originalObjectiveId = player.QuestProgress[0].Objectives[0].ObjectiveId;

        Assert.That(originalObjectiveId, Is.Not.Empty);

        quest.Objectives = [quest.Objectives[1], quest.Objectives[0]];

        await _service.OnMobileKilledAsync(player, CreateMobile((Serial)0x00002002u, "sewer_rat"));

        var objective = player.QuestProgress[0].Objectives.Single(entry => entry.ObjectiveId == originalObjectiveId);

        Assert.Multiple(() =>
        {
            Assert.That(objective.CurrentAmount, Is.EqualTo(1));
            Assert.That(objective.IsCompleted, Is.True);
            Assert.That(player.QuestProgress[0].Objectives.Single(entry => entry.ObjectiveId != originalObjectiveId).CurrentAmount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task OnMobileKilledAsync_WhenKillObjectiveMatches_ShouldAdvanceOnlyKillProgress()
    {
        var player = CreatePlayer();
        var npc = CreateMobile((Serial)0x00002001u, "farmer_npc");
        var backpack = CreateBackpack();
        _characterService.Backpack = backpack;
        _questTemplateService.Upsert(
            new()
            {
                Id = "starter.rat_and_bandage",
                Name = "Rat And Bandage",
                Category = "starter",
                Description = "Kill a rat and gather bandages.",
                QuestGiverTemplateIds = ["farmer_npc"],
                CompletionNpcTemplateIds = ["farmer_npc"],
                Repeatable = false,
                MaxActivePerCharacter = 10,
                Objectives =
                [
                    new()
                    {
                        Type = QuestObjectiveType.Kill,
                        MobileTemplateIds = ["sewer_rat"],
                        Amount = 1
                    },
                    new()
                    {
                        Type = QuestObjectiveType.Collect,
                        ItemTemplateId = "bandage",
                        Amount = 5
                    }
                ]
            }
        );

        _ = await _service.AcceptAsync(player, npc, "starter.rat_and_bandage");
        await _service.OnMobileKilledAsync(player, CreateMobile((Serial)0x00002002u, "sewer_rat"));

        Assert.Multiple(() =>
        {
            Assert.That(player.QuestProgress[0].Objectives[0].CurrentAmount, Is.EqualTo(1));
            Assert.That(player.QuestProgress[0].Objectives[0].IsCompleted, Is.True);
            Assert.That(player.QuestProgress[0].Objectives[1].CurrentAmount, Is.EqualTo(0));
            Assert.That(player.QuestProgress[0].Status, Is.EqualTo(QuestProgressStatusType.Active));
        });
    }

    [Test]
    public async Task ReevaluateInventoryAsync_WhenCollectObjectiveIsSatisfied_ShouldMarkQuestReadyToTurnIn()
    {
        var player = CreatePlayer();
        var npc = CreateMobile((Serial)0x00002001u, "farmer_npc");
        var bandages = CreateItem((Serial)0x40001002u, "bandage", 5);
        var pouch = CreateContainer((Serial)0x40001003u, bandages);
        _characterService.Backpack = CreateBackpack(pouch);
        _questTemplateService.Upsert(
            new()
            {
                Id = "starter.bandage_collection",
                Name = "Bandage Collection",
                Category = "starter",
                Description = "Collect bandages.",
                QuestGiverTemplateIds = ["farmer_npc"],
                CompletionNpcTemplateIds = ["farmer_npc"],
                Repeatable = false,
                MaxActivePerCharacter = 10,
                Objectives =
                [
                    new()
                    {
                        Type = QuestObjectiveType.Collect,
                        ItemTemplateId = "bandage",
                        Amount = 5
                    }
                ]
            }
        );

        _ = await _service.AcceptAsync(player, npc, "starter.bandage_collection");
        await _service.ReevaluateInventoryAsync(player);

        Assert.Multiple(() =>
        {
            Assert.That(player.QuestProgress[0].Objectives[0].CurrentAmount, Is.EqualTo(5));
            Assert.That(player.QuestProgress[0].Objectives[0].IsCompleted, Is.True);
            Assert.That(player.QuestProgress[0].Status, Is.EqualTo(QuestProgressStatusType.ReadyToTurnIn));
        });
    }

    [Test]
    public async Task GetJournalAsync_WhenQuestIsCompleted_ShouldExcludeIt()
    {
        var player = CreatePlayer();
        var npc = CreateMobile((Serial)0x00002001u, "farmer_npc");
        _questTemplateService.Upsert(CreateKillQuest("starter.rat_hunt"));

        _ = await _service.AcceptAsync(player, npc, "starter.rat_hunt");
        player.QuestProgress[0].Status = QuestProgressStatusType.Completed;
        player.QuestProgress[0].CompletedAtUtc = DateTime.UtcNow;

        var journal = await _service.GetJournalAsync(player);

        Assert.That(journal, Is.Empty);
    }

    [Test]
    public async Task TryCompleteAsync_WhenDeliverObjectiveIsReady_ShouldConsumeItemsAndCompleteQuest()
    {
        var player = CreatePlayer();
        var npc = CreateMobile((Serial)0x00002001u, "farmer_npc");
        var apples = CreateItem((Serial)0x40001004u, "apple", 3);
        var backpack = CreateBackpack(apples);
        _characterService.Backpack = backpack;
        _questTemplateService.Upsert(
            new()
            {
                Id = "starter.apple_delivery",
                Name = "Apple Delivery",
                Category = "starter",
                Description = "Deliver apples.",
                QuestGiverTemplateIds = ["farmer_npc"],
                CompletionNpcTemplateIds = ["farmer_npc"],
                Repeatable = false,
                MaxActivePerCharacter = 10,
                Objectives =
                [
                    new()
                    {
                        Type = QuestObjectiveType.Deliver,
                        ItemTemplateId = "apple",
                        Amount = 3
                    }
                ]
            }
        );

        _ = await _service.AcceptAsync(player, npc, "starter.apple_delivery");
        await _service.ReevaluateInventoryAsync(player);
        var completed = await _service.TryCompleteAsync(player, npc, "starter.apple_delivery");

        Assert.Multiple(() =>
        {
            Assert.That(completed, Is.True);
            Assert.That(player.QuestProgress[0].Status, Is.EqualTo(QuestProgressStatusType.Completed));
            Assert.That(player.QuestProgress[0].CompletedAtUtc, Is.Not.Null);
            Assert.That(backpack.Items, Is.Empty);
            Assert.That(_itemService.DeletedItemIds, Is.EqualTo([apples.Id]));
        });
    }

    [Test]
    public async Task TryCompleteAsync_WhenDeliverItemsAreMissingAtTurnIn_ShouldPersistQuestBackToActive()
    {
        var player = CreatePlayer();
        var npc = CreateMobile((Serial)0x00002001u, "farmer_npc");
        var apples = CreateItem((Serial)0x40001004u, "apple", 3);
        var backpack = CreateBackpack(apples);
        _characterService.Backpack = backpack;
        _questTemplateService.Upsert(
            new()
            {
                Id = "starter.apple_delivery",
                Name = "Apple Delivery",
                Category = "starter",
                Description = "Deliver apples.",
                QuestGiverTemplateIds = ["farmer_npc"],
                CompletionNpcTemplateIds = ["farmer_npc"],
                Repeatable = false,
                MaxActivePerCharacter = 10,
                Objectives =
                [
                    new()
                    {
                        Type = QuestObjectiveType.Deliver,
                        ItemTemplateId = "apple",
                        Amount = 3
                    }
                ]
            }
        );

        _ = await _service.AcceptAsync(player, npc, "starter.apple_delivery");
        await _service.ReevaluateInventoryAsync(player);
        backpack.RemoveItem(apples.Id);

        var completed = await _service.TryCompleteAsync(player, npc, "starter.apple_delivery");

        Assert.Multiple(() =>
        {
            Assert.That(completed, Is.False);
            Assert.That(player.QuestProgress[0].Status, Is.EqualTo(QuestProgressStatusType.Active));
            Assert.That(player.QuestProgress[0].Objectives[0].CurrentAmount, Is.EqualTo(0));
            Assert.That(_mobileService.UpsertedMobiles, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task TryCompleteAsync_WhenQuestHasRewards_ShouldCreditGoldAndCreateRewardItems()
    {
        var player = CreatePlayer();
        var npc = CreateMobile((Serial)0x00002001u, "farmer_npc");
        var apples = CreateItem((Serial)0x40001005u, "apple", 3);
        var backpack = CreateBackpack(apples);
        _characterService.Backpack = backpack;
        _questTemplateService.Upsert(
            new()
            {
                Id = "starter.apple_delivery",
                Name = "Apple Delivery",
                Category = "starter",
                Description = "Deliver apples.",
                QuestGiverTemplateIds = ["farmer_npc"],
                CompletionNpcTemplateIds = ["farmer_npc"],
                Repeatable = false,
                MaxActivePerCharacter = 10,
                Objectives =
                [
                    new()
                    {
                        Type = QuestObjectiveType.Deliver,
                        ItemTemplateId = "apple",
                        Amount = 3
                    }
                ],
                Rewards =
                [
                    new()
                    {
                        Gold = 150,
                        Items =
                        [
                            new()
                            {
                                ItemTemplateId = "bandage",
                                Amount = 10
                            }
                        ]
                    }
                ]
            }
        );

        _ = await _service.AcceptAsync(player, npc, "starter.apple_delivery");
        await _service.ReevaluateInventoryAsync(player);
        var completed = await _service.TryCompleteAsync(player, npc, "starter.apple_delivery");

        var gold = backpack.Items.Single(static item => HasTemplateId(item, "gold"));
        var bandageReward = backpack.Items.Single(static item => HasTemplateId(item, "bandage"));

        Assert.Multiple(() =>
        {
            Assert.That(completed, Is.True);
            Assert.That(gold.Amount, Is.EqualTo(150));
            Assert.That(bandageReward.Amount, Is.EqualTo(10));
            Assert.That(_itemService.CreatedItemIds, Does.Contain(gold.Id));
            Assert.That(_itemService.CreatedItemIds, Does.Contain(bandageReward.Id));
        });
    }

    private static UOMobileEntity CreatePlayer()
        => new()
        {
            Id = (Serial)0x00000001u,
            IsPlayer = true,
            IsAlive = true
        };

    private static UOMobileEntity CreateMobile(Serial id, string templateId)
    {
        var mobile = new UOMobileEntity
        {
            Id = id,
            IsAlive = true
        };
        mobile.SetCustomString(MobileCustomParamKeys.Template.TemplateId, templateId);

        return mobile;
    }

    private static QuestTemplateDefinition CreateKillQuest(string id)
        => new()
        {
            Id = id,
            Name = "Rat Hunt",
            Category = "starter",
            Description = "Kill rats.",
            QuestGiverTemplateIds = ["farmer_npc"],
            CompletionNpcTemplateIds = ["farmer_npc"],
            Repeatable = false,
            MaxActivePerCharacter = 10,
            Objectives =
            [
                new()
                {
                    Type = QuestObjectiveType.Kill,
                    MobileTemplateIds = ["sewer_rat"],
                    Amount = 3
                }
            ]
        };

    private static UOItemEntity CreateBackpack(params UOItemEntity[] items)
    {
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40000001u,
            ItemId = 0x0E75,
            Name = "Backpack"
        };
        backpack.SetCustomString(ItemCustomParamKeys.Item.TemplateId, "backpack");

        for (var index = 0; index < items.Length; index++)
        {
            backpack.AddItem(items[index], new(index + 1, index + 1));
        }

        return backpack;
    }

    private static UOItemEntity CreateContainer(Serial id, params UOItemEntity[] items)
    {
        var container = new UOItemEntity
        {
            Id = id,
            ItemId = 0x0E76,
            Name = "Pouch"
        };
        container.SetCustomString(ItemCustomParamKeys.Item.TemplateId, "pouch");

        for (var index = 0; index < items.Length; index++)
        {
            container.AddItem(items[index], new(index + 1, index + 1));
        }

        return container;
    }

    private static UOItemEntity CreateItem(Serial id, string templateId, int amount)
    {
        var item = new UOItemEntity
        {
            Id = id,
            ItemId = templateId == "gold" ? 0x0EED : 0x0F7A,
            Name = templateId,
            Amount = amount,
            IsStackable = true
        };
        item.SetCustomString(ItemCustomParamKeys.Item.TemplateId, templateId);

        return item;
    }

    private static bool HasTemplateId(UOItemEntity item, string templateId)
        => item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var value) &&
           string.Equals(value, templateId, StringComparison.OrdinalIgnoreCase);

    private sealed class FakeCharacterService : ICharacterService
    {
        public UOItemEntity? Backpack { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
        {
            _ = accountId;
            _ = characterId;

            return Task.FromResult(true);
        }

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
        {
            _ = characterId;
            _ = shirtHue;
            _ = pantsHue;

            return Task.CompletedTask;
        }

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(character.Id);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult(Backpack);
        }

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult<UOItemEntity?>(null);
        }

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
        {
            _ = characterId;

            return Task.FromResult<UOMobileEntity?>(null);
        }

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
        {
            _ = accountId;

            return Task.FromResult(new List<UOMobileEntity>());
        }

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
        {
            _ = accountId;
            _ = characterId;

            return Task.FromResult(true);
        }
    }

    private sealed class FakeMobileService : IMobileService
    {
        public List<Serial> UpsertedMobiles { get; } = [];

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            UpsertedMobiles.Add(mobile.Id);

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = id;
            _ = cancellationToken;

            throw new NotSupportedException();
        }

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = id;
            _ = cancellationToken;

            throw new NotSupportedException();
        }

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(int mapId, int sectorX, int sectorY, CancellationToken cancellationToken = default)
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;
            _ = cancellationToken;

            throw new NotSupportedException();
        }

        public Task<UOMobileEntity> SpawnFromTemplateAsync(string templateId, Point3D location, int mapId, Serial? accountId = null, CancellationToken cancellationToken = default)
        {
            _ = templateId;
            _ = location;
            _ = mapId;
            _ = accountId;
            _ = cancellationToken;

            throw new NotSupportedException();
        }

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(string templateId, Point3D location, int mapId, Serial? accountId = null, CancellationToken cancellationToken = default)
        {
            _ = templateId;
            _ = location;
            _ = mapId;
            _ = accountId;
            _ = cancellationToken;

            throw new NotSupportedException();
        }
    }

    private sealed class FakeItemService : IItemService
    {
        public List<Serial> CreatedItemIds { get; } = [];
        public List<Serial> DeletedItemIds { get; } = [];
        public List<Serial> UpsertedItemIds { get; } = [];

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
        {
            foreach (var item in items)
            {
                UpsertedItemIds.Add(item.Id);
            }

            return Task.CompletedTask;
        }

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
        {
            _ = generateNewSerial;

            return item;
        }

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
        {
            _ = itemId;
            _ = generateNewSerial;

            throw new NotSupportedException();
        }

        public Task<Serial> CreateItemAsync(UOItemEntity item)
        {
            CreatedItemIds.Add(item.Id);

            return Task.FromResult(item.Id);
        }

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            DeletedItemIds.Add(itemId);

            return Task.FromResult(true);
        }

        public Task<Moongate.Server.Data.Items.DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        {
            _ = itemId;
            _ = location;
            _ = mapId;
            _ = sessionId;

            throw new NotSupportedException();
        }

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
        {
            _ = itemId;
            _ = mobileId;
            _ = layer;

            throw new NotSupportedException();
        }

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;

            throw new NotSupportedException();
        }

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
        {
            _ = itemId;

            throw new NotSupportedException();
        }

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
        {
            _ = containerId;

            throw new NotSupportedException();
        }

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
        {
            _ = itemId;
            _ = containerId;
            _ = position;
            _ = sessionId;

            throw new NotSupportedException();
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        {
            _ = itemId;
            _ = location;
            _ = mapId;
            _ = sessionId;

            throw new NotSupportedException();
        }

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
        {
            _ = itemTemplateId;

            throw new NotSupportedException();
        }

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
        {
            _ = itemId;

            throw new NotSupportedException();
        }

        public Task UpsertItemAsync(UOItemEntity item)
        {
            UpsertedItemIds.Add(item.Id);

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
        {
            foreach (var item in items)
            {
                UpsertedItemIds.Add(item.Id);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeItemFactoryService : IItemFactoryService
    {
        private uint _nextSerial = 0x50000000u;

        public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
            => CreateItem((Serial)_nextSerial++, itemTemplateId, 1);

        public UOItemEntity GetNewBackpack()
            => CreateBackpack();

        public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? definition)
        {
            if (string.Equals(itemTemplateId, "gold", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(itemTemplateId, "bandage", StringComparison.OrdinalIgnoreCase))
            {
                definition = new ItemTemplateDefinition
                {
                    Id = itemTemplateId,
                    Name = itemTemplateId
                };

                return true;
            }

            definition = null;

            return false;
        }
    }
}
