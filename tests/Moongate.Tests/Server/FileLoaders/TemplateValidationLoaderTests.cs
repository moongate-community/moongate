using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Containers;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.FileLoaders;

public class TemplateValidationLoaderTests
{
    [Test]
    public void LoadAsync_WhenMobileHasNoVariants_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "variantless_mobile",
                Name = "Variantless Mobile",
                Category = "test",
                Description = "test"
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileAiBrainIsBlank_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "invalid_ai_brain_mobile",
                Name = "Invalid Ai Brain Mobile",
                Category = "test",
                Description = "test",
                Ai = new()
                {
                    Brain = " "
                },
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x0190
                        }
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileAiFightModeIsInvalid_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "invalid_ai_fight_mode_mobile",
                Name = "Invalid Ai Fight Mode Mobile",
                Category = "test",
                Description = "test",
                Ai = new()
                {
                    Brain = "ai_guard",
                    FightMode = "reckless"
                },
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x0190
                        }
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileAiFightModeIsNull_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "null_ai_fight_mode_mobile",
                Name = "Null Ai Fight Mode Mobile",
                Category = "test",
                Description = "test",
                Ai = new()
                {
                    Brain = "ai_guard",
                    FightMode = null!
                },
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x0190
                        }
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileAiRangePerceptionIsNonPositive_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "invalid_ai_range_perception_mobile",
                Name = "Invalid Ai Range Perception Mobile",
                Category = "test",
                Description = "test",
                Ai = new()
                {
                    Brain = "ai_guard",
                    RangePerception = 0
                },
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x0190
                        }
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileAiRangeFightIsNegative_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "invalid_ai_range_fight_mobile",
                Name = "Invalid Ai Range Fight Mobile",
                Category = "test",
                Description = "test",
                Ai = new()
                {
                    Brain = "ai_guard",
                    RangeFight = -1
                },
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x0190
                        }
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenQuestReferencesMissingQuestGiverTemplate_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        var questTemplateService = new QuestTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        questTemplateService.Upsert(
            new()
            {
                Id = "new_haven.rat_hunt",
                Name = "Rat Hunt",
                Category = "starter",
                Description = "test",
                QuestGiverTemplateIds = ["missing_farmer_npc"],
                CompletionNpcTemplateIds = ["missing_farmer_npc"],
                Repeatable = false,
                MaxActivePerCharacter = 1,
                Objectives =
                [
                    new()
                    {
                        Type = QuestObjectiveType.Kill,
                        MobileTemplateIds = ["sewer_rat"],
                        Amount = 10
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService,
            questTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenQuestRewardItemTemplateIsMissing_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        var questTemplateService = new QuestTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "farmer_npc",
                Name = "Farmer",
                Category = "test",
                Description = "test",
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x0190
                        }
                    }
                ]
            }
        );
        mobileService.Upsert(
            new()
            {
                Id = "sewer_rat",
                Name = "Rat",
                Category = "test",
                Description = "test",
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x00EE
                        }
                    }
                ]
            }
        );
        questTemplateService.Upsert(
            new()
            {
                Id = "new_haven.rat_hunt",
                Name = "Rat Hunt",
                Category = "starter",
                Description = "test",
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
                        Amount = 10
                    }
                ],
                Rewards =
                [
                    new()
                    {
                        Gold = 100,
                        Items =
                        [
                            new()
                            {
                                ItemTemplateId = "missing_bandage",
                                Amount = 5
                            }
                        ]
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService,
            questTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileAiRangeFightIsZero_ShouldNotThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "valid_ai_range_fight_mobile",
                Name = "Valid Ai Range Fight Mobile",
                Category = "test",
                Description = "test",
                Ai = new()
                {
                    Brain = "ai_guard",
                    FightMode = "closest",
                    RangePerception = 1,
                    RangeFight = 0
                },
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x0190
                        }
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.That(async () => await loader.LoadAsync(), Throws.Nothing);
    }

    [Test]
    public void LoadAsync_WhenVariantHasNoBody_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "bodyless_mobile",
                Name = "Bodyless Mobile",
                Category = "test",
                Description = "test",
                Variants =
                [
                    new()
                    {
                        Name = "default"
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileParamsContainInvalidHue_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "invalid_mobile_params",
                Name = "Invalid Mobile Params",
                Category = "test",
                Description = "test",
                Params = new Dictionary<string, ItemTemplateParamDefinition>
                {
                    ["marker_hue"] = new()
                    {
                        Type = ItemTemplateParamType.Hue,
                        Value = "not-a-hue"
                    }
                },
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x0190
                        }
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenVariantEquipmentParamsContainInvalidSerial_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        itemService.Upsert(
            new()
            {
                Id = "robe",
                Name = "Robe",
                Category = "clothing",
                Description = "robe",
                ItemId = "0x1F03",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.robe",
                Weight = 1
            }
        );

        mobileService.Upsert(
            new()
            {
                Id = "invalid_equipment_params",
                Name = "Invalid Equipment Params",
                Category = "test",
                Description = "test",
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x0190
                        },
                        Equipment =
                        [
                            new()
                            {
                                Layer = ItemLayerType.OuterTorso,
                                ItemTemplateId = "robe",
                                Params = new Dictionary<string, ItemTemplateParamDefinition>
                                {
                                    ["owner_id"] = new()
                                    {
                                        Type = ItemTemplateParamType.Serial,
                                        Value = "not-a-serial"
                                    }
                                }
                            }
                        ]
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenAdditiveLootUsesWeight_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        itemService.Upsert(
            new()
            {
                Id = "gold",
                Name = "Gold",
                Category = "misc",
                Description = "gold",
                ItemId = "0x0EED",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "none",
                Weight = 0.01M,
                Tags = ["currency"]
            }
        );

        lootTemplateService.Upsert(
            new()
            {
                Id = "undead.zombie",
                Name = "Zombie Loot",
                Category = "loot",
                Description = string.Empty,
                Mode = LootTemplateMode.Additive,
                Entries =
                [
                    new()
                    {
                        ItemTemplateId = "gold",
                        Weight = 10
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenBookTemplateReferencesMissingBookFile_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        itemService.Upsert(
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
                Tags = ["book"],
                BookId = "missing_book"
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenContainerItemHasMissingLayoutId_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        itemService.Upsert(
            new()
            {
                Id = "item.container",
                Name = "Container",
                Category = "container",
                Description = "container",
                ItemId = "0x0E76",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "none",
                Weight = 1,
                Tags = ["container"]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenItemReferencesMissingLootTable_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);
        ContainerLayoutSystem.ContainerSizesById["wooden_chest"] = new("wooden_chest", 7, 4, "Wooden Chest");

        itemService.Upsert(
            new()
            {
                Id = "item.loot_chest",
                Name = "Loot Chest",
                Category = "containers",
                Description = "loot chest",
                ItemId = "0x0E40",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.loot_chest",
                Weight = 1,
                Tags = ["container"],
                ContainerLayoutId = "wooden_chest",
                LootTables = ["missing_loot"]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileReferencesMissingLootTable_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "loot_mobile",
                Name = "Loot Mobile",
                Category = "test",
                Description = "test",
                LootTables = ["missing_loot"],
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x0190,
                            SkinHue = HueSpec.FromValue(0),
                            HairHue = HueSpec.FromValue(0)
                        }
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileReferencesMissingDefaultFaction_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "faction_guard",
                Name = "Faction Guard",
                Category = "guards",
                Description = "guard",
                DefaultFactionId = "missing_faction",
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x11,
                            SkinHue = HueSpec.FromValue(779),
                            HairHue = HueSpec.FromValue(0)
                        }
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileReferencesMissingItem_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "orc",
                Name = "Orc",
                Category = "monsters",
                Description = "orc",
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x11,
                            SkinHue = HueSpec.FromValue(779),
                            HairHue = HueSpec.FromValue(0)
                        },
                        Equipment =
                        [
                            new()
                            {
                                ItemTemplateId = "item.missing",
                                Layer = ItemLayerType.Shirt
                            }
                        ]
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileReferencesMissingSellProfile_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "vendor_missing_profile",
                Name = "Vendor",
                Category = "vendors",
                Description = "vendor",
                SellProfileId = "missing_profile",
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x11,
                            SkinHue = HueSpec.FromValue(779),
                            HairHue = HueSpec.FromValue(0)
                        }
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public async Task LoadAsync_WhenTemplatesAreValid_ShouldNotThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);
        ContainerLayoutSystem.ContainerSizesById["backpack"] = new("backpack", 7, 4, "Backpack");
        factionTemplateService.Upsert(
            new()
            {
                Id = "true_britannians",
                Name = "True Britannians"
            }
        );
        lootTemplateService.Upsert(
            new()
            {
                Id = "minor_treasure",
                Name = "Minor Treasure",
                Category = "loot",
                Description = "test",
                Entries =
                [
                    new()
                    {
                        ItemTemplateId = "item.shirt",
                        Weight = 1,
                        Amount = 1
                    }
                ]
            }
        );

        itemService.Upsert(
            new()
            {
                Id = "item.shirt",
                Name = "Shirt",
                Category = "clothes",
                Description = "shirt",
                ItemId = "0x1517",
                Hue = HueSpec.FromRange(5, 55),
                GoldValue = GoldValueSpec.FromDiceExpression("1d8+8"),
                LootType = LootType.Regular,
                ScriptId = "none",
                Weight = 1,
                Tags = ["container"],
                ContainerLayoutId = "backpack",
                LootTables = ["minor_treasure"]
            }
        );

        mobileService.Upsert(
            new()
            {
                Id = "orc",
                Name = "Orc",
                Category = "monsters",
                Description = "orc",
                DefaultFactionId = "true_britannians",
                Ai = new()
                {
                    Brain = "none",
                    FightMode = "closest",
                    RangePerception = 16,
                    RangeFight = 1
                },
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x11,
                            SkinHue = HueSpec.FromValue(779),
                            HairHue = HueSpec.FromValue(0)
                        },
                        Equipment =
                        [
                            new()
                            {
                                ItemTemplateId = "item.shirt",
                                Layer = ItemLayerType.Shirt
                            }
                        ]
                    }
                ]
            }
        );

        sellProfileService.Upsert(
            new()
            {
                Id = "basic_vendor",
                Name = "Basic Vendor",
                Category = "vendor",
                Description = "basic",
                VendorItems =
                [
                    new()
                    {
                        ItemTemplateId = "item.shirt",
                        Price = 50
                    }
                ]
            }
        );

        mobileService.Upsert(
            new()
            {
                Id = "vendor_orc",
                Name = "Vendor Orc",
                Category = "vendors",
                Description = "vendor",
                SellProfileId = "basic_vendor",
                Ai = new()
                {
                    Brain = "none",
                    FightMode = "closest",
                    RangePerception = 16,
                    RangeFight = 1
                },
                Variants =
                [
                    new()
                    {
                        Name = "default",
                        Appearance = new()
                        {
                            Body = 0x11,
                            SkinHue = HueSpec.FromValue(779),
                            HairHue = HueSpec.FromValue(0)
                        }
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.That(async () => await loader.LoadAsync(), Throws.Nothing);
    }

    [Test]
    public void LoadAsync_WhenWeightedLootUsesAmountRange_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        itemService.Upsert(
            new()
            {
                Id = "gold",
                Name = "Gold",
                Category = "misc",
                Description = "gold",
                ItemId = "0x0EED",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "none",
                Weight = 0.01M,
                Tags = ["currency"]
            }
        );

        lootTemplateService.Upsert(
            new()
            {
                Id = "treasure.small",
                Name = "Treasure Small",
                Category = "loot",
                Description = string.Empty,
                Mode = LootTemplateMode.Weighted,
                Entries =
                [
                    new()
                    {
                        ItemTemplateId = "gold",
                        Weight = 1,
                        AmountMin = 20,
                        AmountMax = 40
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenQuestReferencesMissingMobileTemplate_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        var questTemplateService = new QuestTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        questTemplateService.Upsert(
            new()
            {
                Id = "new_haven.rat_hunt",
                Name = "Rat Hunt",
                Category = "starter",
                Description = "test",
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
                        Amount = 10
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService,
            questTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenQuestRewardReferencesMissingItemTemplate_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        var questTemplateService = new QuestTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        questTemplateService.Upsert(
            new()
            {
                Id = "new_haven.bandage_delivery",
                Name = "Bandage Delivery",
                Category = "starter",
                Description = "test",
                QuestGiverTemplateIds = ["healer_npc"],
                CompletionNpcTemplateIds = ["healer_npc"],
                Repeatable = false,
                MaxActivePerCharacter = 1,
                Objectives =
                [
                    new()
                    {
                        Type = QuestObjectiveType.Deliver,
                        ItemTemplateId = "bandage",
                        Amount = 5
                    }
                ],
                Rewards =
                [
                    new()
                    {
                        Items =
                        [
                            new()
                            {
                                ItemTemplateId = "reward_bandage",
                                Amount = 5
                            }
                        ]
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService,
            questTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [SetUp]
    public void SetUp()
    {
        ContainerLayoutSystem.ContainerSizes.Clear();
        ContainerLayoutSystem.ContainerSizesById.Clear();
    }

    private static BookTemplateService CreateBookTemplateService(string rootPath)
    {
        var directoriesConfig = new DirectoriesConfig(
            rootPath,
            DirectoryType.Data,
            DirectoryType.Templates,
            DirectoryType.Scripts,
            DirectoryType.Save,
            DirectoryType.Logs,
            DirectoryType.Cache
        );

        return new(directoriesConfig, new());
    }
}
