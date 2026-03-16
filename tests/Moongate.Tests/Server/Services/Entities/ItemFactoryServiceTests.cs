using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Entities;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Scripting;
using Moongate.Server.Services.Timing;
using Moongate.Server.Services.World;
using Moongate.Server.Types.World;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Containers;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Entities;

public class ItemFactoryServiceTests
{
    [Test]
    public async Task CreateItemFromTemplate_ShouldApplyDoorFacingOverride_FromTemplateParams()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "metal_door_east",
                Name = "Metal Door",
                Category = "Structure",
                Description = "Door with facing override",
                ItemId = "0x0675",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.door",
                Weight = 5,
                Tags = ["door"],
                Params = new()
                {
                    ["Facing"] = new()
                        { Type = ItemTemplateParamType.String, Value = DoorGenerationFacing.EastCCW.ToString() }
                }
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("metal_door_east");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.ItemId, Is.EqualTo(DoorGenerationFacing.EastCCW.ToItemId(0x0675)));
                Assert.That(item.Direction, Is.EqualTo(DoorGenerationFacing.EastCCW.ToDirectionType()));
                Assert.That(item.TryGetCustomString("door_facing", out var facing), Is.True);
                Assert.That(facing, Is.EqualTo(DoorGenerationFacing.EastCCW.ToString()));
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldApplyTypedParamsToCustomProperties()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "param_item",
                Name = "Param Item",
                Category = "test",
                Description = "test",
                ItemId = "0x1517",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.param_item",
                Weight = 1,
                Params = new()
                {
                    ["label"] = new() { Type = ItemTemplateParamType.String, Value = "hello" },
                    ["linked_id"] = new() { Type = ItemTemplateParamType.Serial, Value = "0x40000010" },
                    ["tint"] = new() { Type = ItemTemplateParamType.Hue, Value = "0x044D" }
                }
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("param_item");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.TryGetCustomString("label", out var label), Is.True);
                Assert.That(label, Is.EqualTo("hello"));
                Assert.That(item.TryGetCustomInteger("linked_id", out var linkedId), Is.True);
                Assert.That(linkedId, Is.EqualTo(0x40000010));
                Assert.That(item.TryGetCustomInteger("tint", out var tint), Is.True);
                Assert.That(tint, Is.EqualTo(0x044D));
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldFallbackToTileNameAndWeight_WhenTemplateUsesDefaults()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "fallback_item",
                Name = string.Empty,
                Category = "test",
                Description = "test",
                ItemId = "0x0E75",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.fallback_item",
                Weight = 0
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("fallback_item");
        var tile = TileData.ItemTable[item.ItemId];

        Assert.Multiple(
            () =>
            {
                Assert.That(item.Name, Is.EqualTo(tile.Name));
                Assert.That(item.Weight, Is.EqualTo(tile.Weight));
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldLeaveTypedCombatStatsAndModifiersNull_WhenTemplateHasNoTypedValues()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "plain_item",
                Name = "Plain Item",
                Category = "test",
                Description = "test",
                ItemId = "0x1517",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.plain_item",
                Weight = 1
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("plain_item");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.CombatStats, Is.Null);
                Assert.That(item.Modifiers, Is.Null);
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldMapTemplateFieldsAndAllocateItemSerial()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "test_item",
                Name = "Test Item",
                Category = "test",
                Description = "test",
                ItemId = "0x1517",
                GumpId = "0x0042",
                Hue = HueSpec.FromValue(77),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.test_item",
                Weight = 6,
                Rarity = ItemRarity.Legendary
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("test_item");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.Id.IsItem, Is.True);
                Assert.That(item.Name, Is.EqualTo("Test Item"));
                Assert.That(item.ItemId, Is.EqualTo(0x1517));
                Assert.That(item.GumpId, Is.EqualTo(0x0042));
                Assert.That(item.Hue, Is.EqualTo(77));
                Assert.That(item.Weight, Is.EqualTo(6));
                Assert.That(item.Rarity, Is.EqualTo(ItemRarity.Legendary));
                Assert.That(item.Visibility, Is.EqualTo(AccountType.Regular));
                Assert.That(item.ScriptId, Is.EqualTo("items.test_item"));
                Assert.That(item.Location, Is.EqualTo(Point3D.Zero));
                Assert.That(item.ParentContainerId, Is.EqualTo(Serial.Zero));
                Assert.That(item.EquippedLayer, Is.Null);
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldMapTypedCombatStatsAndModifiers()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "combat_item",
                Name = "Combat Item",
                Category = "test",
                Description = "test",
                ItemId = "0x13B9",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.combat_item",
                Weight = 1,
                Strength = 40,
                Dexterity = 15,
                Intelligence = 10,
                StrengthAdd = 5,
                DexterityAdd = 3,
                IntelligenceAdd = 2,
                PhysicalResist = 10,
                FireResist = 11,
                ColdResist = 12,
                PoisonResist = 13,
                EnergyResist = 14,
                HitChanceIncrease = 15,
                DefenseChanceIncrease = 16,
                DamageIncrease = 17,
                SwingSpeedIncrease = 18,
                SpellDamageIncrease = 19,
                FasterCasting = 2,
                FasterCastRecovery = 4,
                LowerManaCost = 5,
                LowerReagentCost = 6,
                Luck = 100,
                SpellChanneling = true,
                UsesRemaining = 25,
                LowDamage = 11,
                HighDamage = 13,
                Defense = 15,
                Speed = 30,
                BaseRange = 1,
                MaxRange = 2,
                HitPoints = 45
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("combat_item");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.CombatStats, Is.Not.Null);
                Assert.That(item.CombatStats!.MinStrength, Is.EqualTo(40));
                Assert.That(item.CombatStats.MinDexterity, Is.EqualTo(15));
                Assert.That(item.CombatStats.MinIntelligence, Is.EqualTo(10));
                Assert.That(item.CombatStats.DamageMin, Is.EqualTo(11));
                Assert.That(item.CombatStats.DamageMax, Is.EqualTo(13));
                Assert.That(item.CombatStats.Defense, Is.EqualTo(15));
                Assert.That(item.CombatStats.AttackSpeed, Is.EqualTo(30));
                Assert.That(item.CombatStats.RangeMin, Is.EqualTo(1));
                Assert.That(item.CombatStats.RangeMax, Is.EqualTo(2));
                Assert.That(item.CombatStats.MaxDurability, Is.EqualTo(45));
                Assert.That(item.CombatStats.CurrentDurability, Is.EqualTo(45));

                Assert.That(item.Modifiers, Is.Not.Null);
                Assert.That(item.Modifiers!.StrengthBonus, Is.EqualTo(5));
                Assert.That(item.Modifiers.DexterityBonus, Is.EqualTo(3));
                Assert.That(item.Modifiers.IntelligenceBonus, Is.EqualTo(2));
                Assert.That(item.Modifiers.PhysicalResist, Is.EqualTo(10));
                Assert.That(item.Modifiers.FireResist, Is.EqualTo(11));
                Assert.That(item.Modifiers.ColdResist, Is.EqualTo(12));
                Assert.That(item.Modifiers.PoisonResist, Is.EqualTo(13));
                Assert.That(item.Modifiers.EnergyResist, Is.EqualTo(14));
                Assert.That(item.Modifiers.HitChanceIncrease, Is.EqualTo(15));
                Assert.That(item.Modifiers.DefenseChanceIncrease, Is.EqualTo(16));
                Assert.That(item.Modifiers.DamageIncrease, Is.EqualTo(17));
                Assert.That(item.Modifiers.SwingSpeedIncrease, Is.EqualTo(18));
                Assert.That(item.Modifiers.SpellDamageIncrease, Is.EqualTo(19));
                Assert.That(item.Modifiers.FasterCasting, Is.EqualTo(2));
                Assert.That(item.Modifiers.FasterCastRecovery, Is.EqualTo(4));
                Assert.That(item.Modifiers.LowerManaCost, Is.EqualTo(5));
                Assert.That(item.Modifiers.LowerReagentCost, Is.EqualTo(6));
                Assert.That(item.Modifiers.Luck, Is.EqualTo(100));
                Assert.That(item.Modifiers.SpellChanneling, Is.EqualTo(1));
                Assert.That(item.Modifiers.UsesRemaining, Is.EqualTo(25));
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldMapVisibilityFromTemplate()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "gm_only_item",
                Name = "GM Only Item",
                Category = "test",
                Description = "test",
                ItemId = "0x1517",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.gm_only_item",
                Weight = 1,
                Visibility = AccountType.GameMaster
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("gm_only_item");

        Assert.That(item.Visibility, Is.EqualTo(AccountType.GameMaster));
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldMaterializeWritableBookMetadataFromParams()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "journal_book",
                Name = "Journal",
                Category = "Books",
                Description = "Writable book",
                ItemId = "0x0FF1",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.journal_book",
                Weight = 1,
                Params = new()
                {
                    ["book_title"] = new() { Type = ItemTemplateParamType.String, Value = "Journal" },
                    ["book_author"] = new() { Type = ItemTemplateParamType.String, Value = "Player" },
                    ["book_content"] = new() { Type = ItemTemplateParamType.String, Value = "" },
                    ["writable"] = new() { Type = ItemTemplateParamType.String, Value = "true" }
                }
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("journal_book");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.TryGetCustomBoolean("book_writable", out var writable), Is.True);
                Assert.That(writable, Is.True);
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldPreferBookTemplateReadOnlyFalseOverWritableParam()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "journal_book",
                Name = "Journal",
                Category = "Books",
                Description = "Writable",
                ItemId = "0x0FF1",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.journal_book",
                Weight = 1,
                BookId = "journal",
                Params = new()
                {
                    ["writable"] = new() { Type = ItemTemplateParamType.String, Value = "false" }
                }
            }
        );

        var booksDirectory = Path.Combine(temp.Path, "templates", "books");
        Directory.CreateDirectory(booksDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(booksDirectory, "journal.txt"),
            """
            [Title] Journal
            [Author] Scribe
            [ReadOnly] False

            Entry one.
            """
        );

        var directoriesConfig = new DirectoriesConfig(
            temp.Path,
            DirectoryType.Data,
            DirectoryType.Templates,
            DirectoryType.Scripts,
            DirectoryType.Save,
            DirectoryType.Logs,
            DirectoryType.Cache
        );
        var service = new ItemFactoryService(
            templateService,
            persistence,
            new BookTemplateService(directoriesConfig, new())
        );

        var item = service.CreateItemFromTemplate("journal_book");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.TryGetCustomBoolean("book_writable", out var writable), Is.True);
                Assert.That(writable, Is.True);
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldPreferBookTemplateReadOnlyTrueOverWritableParam()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "welcome_book",
                Name = "Welcome",
                Category = "Books",
                Description = "Read only",
                ItemId = "0x0FF0",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.welcome_book",
                Weight = 1,
                BookId = "welcome_player",
                Params = new()
                {
                    ["writable"] = new() { Type = ItemTemplateParamType.String, Value = "true" }
                }
            }
        );

        var booksDirectory = Path.Combine(temp.Path, "templates", "books");
        Directory.CreateDirectory(booksDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(booksDirectory, "welcome_player.txt"),
            """
            [Title] Welcome
            [Author] Archivist
            [ReadOnly] True

            Hello.
            """
        );

        var directoriesConfig = new DirectoriesConfig(
            temp.Path,
            DirectoryType.Data,
            DirectoryType.Templates,
            DirectoryType.Scripts,
            DirectoryType.Save,
            DirectoryType.Logs,
            DirectoryType.Cache
        );
        var service = new ItemFactoryService(
            templateService,
            persistence,
            new BookTemplateService(directoriesConfig, new())
        );

        var item = service.CreateItemFromTemplate("welcome_book");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.TryGetCustomBoolean("book_writable", out var writable), Is.True);
                Assert.That(writable, Is.False);
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldRenderBookTemplateIntoCustomProperties()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "welcome_book",
                Name = "Welcome Book",
                Category = "books",
                Description = "welcome",
                ItemId = "0x0FF0",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "none",
                Weight = 1,
                BookId = "welcome_player",
                Tags = ["book"]
            }
        );

        var booksDirectory = Path.Combine(temp.Path, "templates", "books");
        Directory.CreateDirectory(booksDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(booksDirectory, "welcome_player.txt"),
            """
            [Title] Welcome To {{ shard.name }}
            [Author] Archivist

            Welcome traveler.
            """
        );

        var directoriesConfig = new DirectoriesConfig(
            temp.Path,
            DirectoryType.Data,
            DirectoryType.Templates,
            DirectoryType.Scripts,
            DirectoryType.Save,
            DirectoryType.Logs,
            DirectoryType.Cache
        );
        var config = new MoongateConfig();
        config.Game.ShardName = "Moongate";
        var bookTemplateService = new BookTemplateService(directoriesConfig, config);

        var service = new ItemFactoryService(templateService, persistence, bookTemplateService);

        var item = service.CreateItemFromTemplate("welcome_book");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.TryGetCustomString("book_id", out var bookId), Is.True);
                Assert.That(bookId, Is.EqualTo("welcome_player"));
                Assert.That(item.TryGetCustomString("book_title", out var title), Is.True);
                Assert.That(title, Is.EqualTo("Welcome To Moongate"));
                Assert.That(item.TryGetCustomString("book_author", out var author), Is.True);
                Assert.That(author, Is.EqualTo("Archivist"));
                Assert.That(item.TryGetCustomString("book_content", out var content), Is.True);
                Assert.That(content, Does.Contain("Welcome traveler."));
                Assert.That(item.TryGetCustomBoolean("book_writable", out var writable), Is.False);
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldResolveContainerGumpIdFromContainerDefinitions_WhenTemplateGumpIsMissing()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "gm_like_bag",
                Name = "GM Like Bag",
                Category = "gm",
                Description = "test",
                ItemId = "0x0E76",
                ContainerLayoutId = "bag",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.gm_like_bag",
                Weight = 1
            }
        );

        try
        {
            ContainerLayoutSystem.ContainerBagDefsByItemId[0x0E76] = new()
            {
                Id = "item_0e76",
                ItemId = 0x0E76,
                Name = "Bag 0x0E76",
                Width = 6,
                Height = 6,
                GumpId = 0x003D
            };

            var service = new ItemFactoryService(templateService, persistence);
            var item = service.CreateItemFromTemplate("gm_like_bag");

            Assert.That(item.GumpId, Is.EqualTo(0x003D));
        }
        finally
        {
            _ = ContainerLayoutSystem.ContainerBagDefsByItemId.Remove(0x0E76);
        }
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldResolveSnakeCaseFallback_FromPascalCase()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "barred_metal_door",
                Name = "Barred Metal Door",
                Category = "structure",
                Description = "test",
                ItemId = "0x0685",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.door",
                Weight = 5
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("BarredMetalDoor");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.Name, Is.EqualTo("Barred Metal Door"));
                Assert.That(item.ItemId, Is.EqualTo(0x0685));
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldSetStackableFromTileFlags()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "gold_item",
                Name = "Gold",
                Category = "test",
                Description = "test",
                ItemId = "0x0EED",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.gold_item",
                Weight = 1
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("gold_item");
        var expected = TileData.ItemTable[item.ItemId][UOTileFlag.Generic];

        Assert.That(item.IsStackable, Is.EqualTo(expected));
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldStoreFlippableItemIdsAsCustomProperty()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "door_item",
                Name = "Door Item",
                Category = "test",
                Description = "test",
                ItemId = "0x0675",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.door",
                Weight = 1,
                FlippableItemIds = ["0x0675", "0x0676", "0x0677"]
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("door_item");

        Assert.That(item.TryGetCustomString("flippable_item_ids", out var value), Is.True);
        Assert.That(value, Is.EqualTo("0x0675,0x0676,0x0677"));
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldThrow_WhenSerialParamIsInvalid()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "invalid_serial_param_item",
                Name = "Invalid Serial Param Item",
                Category = "test",
                Description = "test",
                ItemId = "0x1517",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.invalid_serial_param_item",
                Weight = 1,
                Params = new()
                {
                    ["linked_id"] = new() { Type = ItemTemplateParamType.Serial, Value = "not_a_serial" }
                }
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        Assert.That(
            () => service.CreateItemFromTemplate("invalid_serial_param_item"),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("invalid serial param")
        );
    }

    [TestCase(null), TestCase(""), TestCase(" ")]
    public async Task CreateItemFromTemplate_ShouldThrow_WhenTemplateIdIsInvalid(string? templateId)
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        var service = new ItemFactoryService(templateService, persistence);

        Assert.That(
            () => service.CreateItemFromTemplate(templateId!),
            Throws.InstanceOf<ArgumentException>()
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldThrow_WhenTemplateIsMissing()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        var service = new ItemFactoryService(templateService, persistence);

        Assert.That(
            () => service.CreateItemFromTemplate("missing_template"),
            Throws.TypeOf<InvalidOperationException>()
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_WhenTemplateIsDyeable_ShouldPersistDyeableFlag()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "dyeable_shoes",
                Name = "Shoes",
                Category = "Clothing",
                Description = "Dyeable shoes",
                ItemId = "0x170F",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "none",
                Weight = 1,
                Dyeable = true
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("dyeable_shoes");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.TryGetCustomBoolean("dyeable", out var dyeable), Is.True);
                Assert.That(dyeable, Is.True);
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_WhenWritableBookHasNoExplicitMetadata_ShouldInitializeBlankBookFields()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "writable_book",
                Name = "Writable Book",
                Category = "Books",
                Description = "Blank writable book",
                ItemId = "0x0FF0",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "none",
                Weight = 1,
                Params = new()
                {
                    ["writable"] = new() { Type = ItemTemplateParamType.String, Value = "true" }
                }
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("writable_book");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.TryGetCustomBoolean("book_writable", out var writable), Is.True);
                Assert.That(writable, Is.True);
                Assert.That(item.TryGetCustomString("book_title", out var title), Is.True);
                Assert.That(title, Is.Empty);
                Assert.That(item.TryGetCustomString("book_author", out var author), Is.True);
                Assert.That(author, Is.Empty);
                Assert.That(item.TryGetCustomString("book_content", out var content), Is.True);
                Assert.That(content, Is.Empty);
                Assert.That(item.TryGetCustomInteger("book_pages", out var pages), Is.True);
                Assert.That(pages, Is.EqualTo(20));
            }
        );
    }

    [Test]
    public async Task GetNewBackpack_ShouldUseFallback_WhenTemplateIsMissing()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        var service = new ItemFactoryService(templateService, persistence);

        var backpack = service.GetNewBackpack();

        Assert.Multiple(
            () =>
            {
                Assert.That(backpack.Id.IsItem, Is.True);
                Assert.That(backpack.Name, Is.EqualTo("Backpack"));
                Assert.That(backpack.ItemId, Is.EqualTo(0x0E75));
                Assert.That(backpack.Hue, Is.EqualTo(0));
                Assert.That(backpack.ScriptId, Is.EqualTo("none"));
                Assert.That(backpack.IsStackable, Is.False);
            }
        );
    }

    [Test]
    public async Task GetNewBackpack_ShouldUseTemplate_WhenAvailable()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "backpack",
                Name = "Template Backpack",
                Category = "containers",
                Description = "Backpack",
                ItemId = "0x0E75",
                Hue = HueSpec.FromValue(33),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.backpack",
                Weight = 2
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var backpack = service.GetNewBackpack();

        Assert.Multiple(
            () =>
            {
                Assert.That(backpack.Id.IsItem, Is.True);
                Assert.That(backpack.Name, Is.EqualTo("Template Backpack"));
                Assert.That(backpack.ItemId, Is.EqualTo(0x0E75));
                Assert.That(backpack.Hue, Is.EqualTo(33));
                Assert.That(backpack.Weight, Is.EqualTo(2));
                Assert.That(backpack.ScriptId, Is.EqualTo("items.backpack"));
            }
        );
    }

    [Test]
    public async Task TryGetItemTemplate_ShouldResolveSnakeCaseFallback_FromPascalCase()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "barred_metal_door",
                Name = "Barred Metal Door",
                Category = "structure",
                Description = "test",
                ItemId = "0x0685"
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var found = service.TryGetItemTemplate("BarredMetalDoor", out var template);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(template, Is.Not.Null);
                Assert.That(template!.Id, Is.EqualTo("barred_metal_door"));
            }
        );
    }

    [Test]
    public async Task TryGetItemTemplate_ShouldReturnFalse_WhenTemplateMissing()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        var service = new ItemFactoryService(templateService, persistence);

        var found = service.TryGetItemTemplate("missing_template", out var template);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.False);
                Assert.That(template, Is.Null);
            }
        );
    }

    [Test]
    public async Task TryGetItemTemplate_ShouldReturnTrue_WhenTemplateExists()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "test_item",
                Name = "Test Item",
                Category = "test",
                Description = "test",
                ItemId = "0x1517"
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var found = service.TryGetItemTemplate("test_item", out var template);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(template, Is.Not.Null);
                Assert.That(template!.Id, Is.EqualTo("test_item"));
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
