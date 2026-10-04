using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Ultima.Types;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.UoxItemConverter.Internal;
using Moongate.UoxItemConverter.Tests.TestSupport;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class UoxItemConverterCommandTests : IDisposable
{
    private readonly ConverterTestDirectories _dirs = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string CombinedOutput => _output + _error.ToString();

    public UoxItemConverterCommandTests()
    {
        AppContext.SetSwitch("Tomlyn.TomlSerializer.IsReflectionEnabledByDefault", true);
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
    }

    [Fact]
    public void Run_ABlockWithItsOwnFields_ConvertsEveryMappedField()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [base_torch]
            {
            name=torch
            id=0x0f6b
            movable=1
            color=0x0010
            weightmax=10
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        var item = Assert.Single(file!.Item);
        Assert.Equal("base_torch", item.Id);
        Assert.Equal((uint)0x0f6b, item.ItemId.Value);
        Assert.Equal("torch", item.Name);
        Assert.True(item.Movable);
        Assert.Equal(new Hue(0x0010), item.Hue!.Value.Resolve());
        Assert.Equal(10, item.MaxWeight);
        Assert.Null(item.BaseId);
    }

    [Fact]
    public void Run_WhatUox3CallsFood_GetsTheFoodScript()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [base_food]
            {
            type=14
            weight=100
            }
            [0x09d0]
            {
            get=base_food
            name=apple
            id=0x09d0
            }
            [0x0a1e]
            {
            get=base_food
            name=bowl of flour
            id=0x0a1e
            }
            [base_magic_fish]
            {
            get=base_food
            id=0x0dd6
            }
            [0x1f9e]
            {
            name=pitcher of water
            id=0x1f9e
            type=105
            }
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        // UOX3's item type 14 is food, on the block itself or the one it gets its fields from; a drink (type 105) has its own script.
        // Nor is what UOX3 files under food and nobody eats: a bowl of flour, the magic fish.
        var items = ReadItems();
        Assert.True(string.IsNullOrEmpty(items.Values.Single(item => item.Name == "bowl of flour").ScriptId));
        Assert.True(string.IsNullOrEmpty(items["base_magic_fish"].ScriptId));
        Assert.Equal("food", items.Values.Single(item => item.Name == "apple").ScriptId);
        Assert.Equal("drink", items.Values.Single(item => item.Name == "pitcher of water").ScriptId);
    }

    [Fact]
    public void Run_WhatUox3CallsADrink_GetsTheDrinkScript_ButTheJarOfHoney()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [base_drink]
            {
            type=105
            script=2100
            }
            [0x1f9e]
            {
            get=base_drink
            name=pitcher of water
            id=0x1f9e
            }
            [0x09ec]
            {
            get=base_drink
            name=jar of honey
            id=0x09ec
            }
            [0x09d0]
            {
            name=apple
            id=0x09d0
            type=14
            }
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var items = ReadItems();
        Assert.Equal("drink", items.Values.Single(item => item.Name == "pitcher of water").ScriptId);
        Assert.True(string.IsNullOrEmpty(items.Values.Single(item => item.Name == "jar of honey").ScriptId));
        Assert.Equal("food", items.Values.Single(item => item.Name == "apple").ScriptId);
    }

    [Fact]
    public void Run_TheBaseFields_AreConverted()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [coin]
            {
            name=gold coin
            id=0x0eed
            weight=2
            amount=5
            pileable=1
            layer=21
            value=60 30
            decay=1
            newbie
            custominttag=Level 7
            customstringtag=Owner Mario Rossi
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var item = Assert.Single(TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"))!.Item);
        Assert.Equal(0.02m, item.Weight);
        Assert.Equal(5, item.Amount!.Value.Resolve());
        Assert.True(item.Stackable);
        Assert.Equal(LayerType.Backpack, item.Layer);
        Assert.Equal((60, 30), (item.BuyPrice, item.SellPrice));
        Assert.True(item.Decays);
        Assert.Equal(LootType.Newbied, item.LootType);
        Assert.Equal("7", item.Tags!["Level"]);
        Assert.Equal("Mario Rossi", item.Tags["Owner"]);
    }

    [Theory,
     InlineData("value=40", 40, 40),
     InlineData("", null, null)]
    public void Run_OneValueOrNone_SetsBothPricesOrNeither(string valueLine, int? buy, int? sell)
    {
        _dirs.WriteSource("items.dfn", $$"""
            [lamp]
            {
            id=0x0a22
            {{valueLine}}
            }
            """);

        Assert.Equal(0, Run());
        var item = Assert.Single(TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"))!.Item);
        Assert.Equal((buy, sell), (item.BuyPrice, item.SellPrice));
    }

    [Theory,
     InlineData("movable=1", true),
     InlineData("movable=3", true),
     InlineData("movable=2", false),
     InlineData("movable=0", null),
     InlineData("", null),
     InlineData("decay=0", null)]
    public void Run_MovableFollowsUox3_AndUnsetMeansTiledata(string line, bool? movable)
    {
        _dirs.WriteSource("items.dfn", $$"""
            [lamp]
            {
            id=0x0a22
            {{line}}
            }
            """);

        Assert.Equal(0, Run());
        var item = Assert.Single(TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"))!.Item);

        if (line.StartsWith("decay"))
        {
            Assert.False(item.Decays);
        }
        else
        {
            Assert.Equal(movable, item.Movable);
        }
    }

    [Theory,
     InlineData("", null),
     InlineData("visible=0", AccountType.Regular),
     InlineData("visible=1", AccountType.GameMaster),
     InlineData("visible=2", AccountType.GameMaster),
     InlineData("visible=3", AccountType.GameMaster)]
    public void Run_TheVisibleField_MapsHiddenItemsToGameMasterVisibility(string visibleLine, AccountType? expected)
    {
        _dirs.WriteSource(
            "items.dfn",
            $$"""
            [orcspawn]
            {
            name=Orc Spawner
            id=0x1f13
            {{visibleLine}}
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        Assert.Equal(expected, Assert.Single(file!.Item).Visibility);
    }

    [Fact]
    public void Run_ABlockWithASingleGetTarget_ResolvesBaseIdAgainstTheConvertedParent()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [base_torch]
            {
            name=torch
            id=0x0f6b
            }

            [wall_torch]
            {
            get=base_torch
            name=wall_torch
            id=0x0393
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        var wallTorch = file!.Item.Single(item => item.Id == "wall_torch");
        Assert.Equal("base_torch", wallTorch.BaseId);
    }

    [Fact]
    public void Run_AGetTargetWithNoIdOfItsOwn_IsFlattenedIntoTheChild()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [base_item]
            {
            id=0x0000
            }

            [base_metal]
            {
            get=base_item
            weight=50
            custominttag=Metal 1
            }

            [base_coin]
            {
            get=base_metal
            weight=2
            pileable=1
            decay=1
            custominttag=Coin 1
            }

            [0x0eed]
            {
            get=base_coin
            name=gold coin
            id=0x0eed
            decay=0
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        // base_metal and base_coin have one parent and fields of their own, so they are templates too, but the coin
        // still gets their fields inlined and inherits from the first ancestor with an id= of its own.
        Assert.Equal("base_item", file!.Item.Single(item => item.Id == "base_coin").BaseId);
        var coin = file.Item.Single(item => item.Id == "0x0eed_gold_coin");
        Assert.Equal(0.02m, coin.Weight);
        Assert.True(coin.Stackable);
        Assert.False(coin.Decays);
        Assert.Equal("base_item", coin.BaseId);
        Assert.Equal(("1", "1"), (coin.Tags!["Metal"], coin.Tags["Coin"]));
    }

    [Fact]
    public void Run_AnInheritedName_IsTheTemplateNameButNotPartOfTheId()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [base_sleeves]
            {
            name=studded sleeves
            }

            [0x13dc]
            {
            get=base_sleeves
            id=0x13dc
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        var item = Assert.Single(file!.Item);
        Assert.Equal("0x13dc", item.Id);
        Assert.Equal("studded sleeves", item.Name);
    }

    [Fact]
    public void Run_AGetCycleBetweenBlocksWithNoId_StopsWithoutLooping()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [base_a]
            {
            get=base_b
            weight=1
            }

            [base_b]
            {
            get=base_a
            weight=2
            }

            [lamp]
            {
            get=base_a
            id=0x0a22
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        Assert.Equal(0.01m, Assert.Single(file!.Item).Weight);
    }

    [Fact]
    public void Run_ABlockThatGetsItself_LeavesBaseIdUnset()
    {
        _dirs.WriteSource("items.dfn", "[0x27c2]\n{\nget=0x27c2\nid=0x27c2\n}\n");

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        Assert.Null(Assert.Single(file!.Item).BaseId);
    }

    [Fact]
    public void Run_AGetTargetThatNeverConverted_LeavesBaseIdUnset()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [0x1440]
            {
            gett2a=0x1440_t2a
            }

            [0x1441]
            {
            get=0x1440
            id=0x1441
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        var item = Assert.Single(file!.Item);
        Assert.Equal("0x1441", item.Id);
        Assert.Null(item.BaseId);
    }

    [Fact]
    public void Run_ABlockWithOneParentAndNoIdOfItsOwn_BecomesATemplateInheritingTheGraphic()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [0x0df1]
            {
            id=0x0df1
            name=magic staff
            }

            [glacialstaff]
            {
            get=0x0df1
            name=glacial staff
            color=0x0480
            }

            [frost_staff]
            {
            get=glacialstaff
            name=frost staff
            }

            [plain_staff]
            {
            getlbr=0x0df1
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var items = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"))!
                             .Item.ToDictionary(item => item.Id);
        var glacial = items["glacialstaff"];
        Assert.Equal(("0x0df1_magic_staff", 0u, "glacial staff", "0x0480"), (glacial.BaseId, glacial.ItemId.Value, glacial.Name, glacial.Hue.ToString()));
        Assert.Equal(("0x0df1_magic_staff", "frost staff"), (items["frost_staff"].BaseId, items["frost_staff"].Name));
        Assert.Equal("0x0df1_magic_staff", items["plain_staff"].BaseId);
    }

    [Fact]
    public void Run_ALootEntryNamingABlockWithNoIdOfItsOwn_Resolves()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [0x0df1]
            {
            id=0x0df1
            }

            [glacialstaff]
            {
            get=0x0df1
            name=glacial staff
            }

            [LOOTLIST staffs]
            {
            glacialstaff
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var loot = Assert.Single(
            TomlUtils.DeserializeFromFile<LootTemplateFile>(Path.Combine(_dirs.LootDestinationDirectory, "staffs.toml"))!.Loot
        );
        Assert.Equal("glacialstaff", Assert.Single(loot.Entries).ItemId);
    }

    [Fact]
    public void Run_ABlockWithMultipleGetTargetsAndNoIdOfItsOwn_IsSkipped()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [base_torch]
            {
            id=0x0f6b
            }

            [torch_alias]
            {
            get=base_torch 0x0393
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        var item = Assert.Single(file!.Item);
        Assert.Equal("base_torch", item.Id);
    }

    [Fact]
    public void Run_ANameOnABareHexHeader_PrefixesTheNameWithTheHeader()
    {
        // The header, not the name, is what guarantees uniqueness: UOX3 reuses the same name=
        // across many variants of the same conceptual item (verified against real data: "wooden
        // door" 64 times, "ballista" 239 times), so name= alone would collide.
        _dirs.WriteSource(
            "items.dfn",
            """
            [0x1441]
            {
            name=cutlass_ns
            id=0x1441
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        var item = Assert.Single(file!.Item);
        Assert.Equal("0x1441_cutlass_ns", item.Id);
    }

    [Fact]
    public void Run_ANameWithSpacesOnABareHexHeader_ProducesASnakeCaseId()
    {
        // Real name= values are free text ("pitcher of wine", "bone gloves"): the combined Id goes
        // through ToSnakeCase so it never carries a literal space.
        _dirs.WriteSource(
            "items.dfn",
            """
            [0x1f9b]
            {
            name=pitcher of wine
            id=0x1f9b
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        var item = Assert.Single(file!.Item);
        Assert.Equal("0x1f9b_pitcher_of_wine", item.Id);
    }

    [Fact]
    public void Run_TheSameNameOnDifferentBareHexHeaders_ProducesDistinctIds()
    {
        _dirs.WriteSource(
            "doors.dfn",
            """
            [0x0334]
            {
            name=#
            id=0x0334
            }

            [0x0336]
            {
            name=#
            id=0x0336
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "doors.toml"));
        var ids = file!.Item.Select(item => item.Id).ToArray();
        Assert.Equal(2, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Run_ADirectoryOfSourceFiles_MirrorsTheirRelativePathsUnderDestination()
    {
        _dirs.WriteSource(
            "gear/weapons/swords.dfn",
            """
            [base_katana]
            {
            id=0x13fe
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        Assert.True(File.Exists(Path.Combine(_dirs.DestinationDirectory, "gear", "weapons", "swords.toml")));
    }

    [Fact]
    public void Run_ALootListBlock_ConvertsWeightedEntriesAgainstItemsInTheSameFile()
    {
        _dirs.WriteSource(
            "loot.dfn",
            """
            [0x0f0f]
            {
            id=0x0f0f
            }

            [LOOTLIST eartheleLoot]
            {
            40|blank
            10|0x0f0f
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);

        // Each loot table writes to its own file, named after its own Id, not the source .dfn's.
        var file = TomlUtils.DeserializeFromFile<LootTemplateFile>(
            Path.Combine(_dirs.LootDestinationDirectory, "earthele_loot.toml")
        );
        var loot = Assert.Single(file!.Loot);

        // Real LOOTLIST names are camelCase; the Id goes through ToSnakeCase like everything else.
        Assert.Equal("earthele_loot", loot.Id);
        Assert.Equal(2, loot.Entries.Count);

        var blankEntry = loot.Entries.Single(e => e.Weight == 40);
        Assert.Null(blankEntry.ItemId);
        Assert.Null(blankEntry.LootTemplateId);

        var itemEntry = loot.Entries.Single(e => e.Weight == 10);
        Assert.Equal("0x0f0f", itemEntry.ItemId);
        Assert.Null(itemEntry.LootTemplateId);
    }

    [Fact]
    public void Run_ALootEntryForAnItemWithAName_CarriesThatNameAsAComment()
    {
        // A non-bare-hex header's own Id never carries name= (only a bare-hex header's does, see
        // Run_ANameOnABareHexHeader_PrefixesTheNameWithTheHeader), so this is the case where a
        // Comment is not just redundant with the Id: "raw_iron_ore" alone does not say "iron ore".
        _dirs.WriteSource(
            "loot.dfn",
            """
            [raw_iron_ore]
            {
            id=0x19b7
            name=iron ore
            }

            [0x0f81]
            {
            id=0x0f81
            }

            [LOOTLIST eartheleLoot]
            {
            10|raw_iron_ore
            10|0x0f81
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<LootTemplateFile>(
            Path.Combine(_dirs.LootDestinationDirectory, "earthele_loot.toml")
        );
        var loot = Assert.Single(file!.Loot);
        var namedEntry = loot.Entries.Single(e => e.ItemId == "raw_iron_ore");
        Assert.Equal("iron ore", namedEntry.Comment);

        var unnamedEntry = loot.Entries.Single(e => e.ItemId == "0x0f81");
        Assert.Null(unnamedEntry.Comment);
    }

    [Fact]
    public void Run_ALootEntryWithNoWeightPrefix_DefaultsToWeightOne()
    {
        _dirs.WriteSource(
            "loot.dfn",
            """
            [0x0f0f]
            {
            id=0x0f0f
            }

            [LOOTLIST unweighted]
            {
            0x0f0f
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<LootTemplateFile>(
            Path.Combine(_dirs.LootDestinationDirectory, "unweighted.toml")
        );
        var entry = Assert.Single(Assert.Single(file!.Loot).Entries);
        Assert.Equal(1, entry.Weight);
    }

    [Fact]
    public void Run_ANestedLootListReference_ResolvesLootTemplateIdAndAmount()
    {
        _dirs.WriteSource(
            "loot.dfn",
            """
            [LOOTLIST randomgems]
            {
            10|blank
            }

            [LOOTLIST eartheleLoot]
            {
            10|LOOTLIST=randomgems,2
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);

        // randomgems and eartheleLoot are two tables from the same source .dfn, but each still
        // writes to its own file: randomgems.toml and earthele_loot.toml.
        var file = TomlUtils.DeserializeFromFile<LootTemplateFile>(
            Path.Combine(_dirs.LootDestinationDirectory, "earthele_loot.toml")
        );
        var eartheleLoot = Assert.Single(file!.Loot);
        var entry = Assert.Single(eartheleLoot.Entries);
        Assert.Equal("randomgems", entry.LootTemplateId);
        Assert.Null(entry.ItemId);
        Assert.Equal(2, entry.Amount.Resolve());
    }

    [Fact]
    public void Run_MultipleLootListBlocksInTheSameFile_EachWriteItsOwnFile()
    {
        // Reviewing or hand-editing one loot table has no reason to load every other table defined
        // in the same source .dfn alongside it, so each gets its own file under --loot-destination.
        _dirs.WriteSource(
            "loot.dfn",
            """
            [LOOTLIST randomgems]
            {
            10|blank
            }

            [LOOTLIST eartheleLoot]
            {
            10|blank
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        Assert.True(File.Exists(Path.Combine(_dirs.LootDestinationDirectory, "randomgems.toml")));
        Assert.True(File.Exists(Path.Combine(_dirs.LootDestinationDirectory, "earthele_loot.toml")));
        Assert.False(File.Exists(Path.Combine(_dirs.LootDestinationDirectory, "loot.toml")));
    }

    [Fact]
    public void Run_ALootEntryPointingAtNothingResolvable_IsSkipped()
    {
        _dirs.WriteSource(
            "loot.dfn",
            """
            [LOOTLIST eartheleLoot]
            {
            10|LOOTLIST=nonexistent
            10|0x9999
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        Assert.Contains("2 loot entry/entries", CombinedOutput, StringComparison.Ordinal);
        var file = TomlUtils.DeserializeFromFile<LootTemplateFile>(
            Path.Combine(_dirs.LootDestinationDirectory, "earthele_loot.toml")
        );
        Assert.Empty(Assert.Single(file!.Loot).Entries);
    }

    [Fact]
    public void Run_ALootEntryWithAMinMaxAmount_ParsesARangeSpec()
    {
        _dirs.WriteSource(
            "loot.dfn",
            """
            [0x0f0f]
            {
            id=0x0f0f
            }

            [LOOTLIST eartheleLoot]
            {
            10|0x0f0f,1 3
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<LootTemplateFile>(
            Path.Combine(_dirs.LootDestinationDirectory, "earthele_loot.toml")
        );
        var entry = Assert.Single(Assert.Single(file!.Loot).Entries);
        var resolves = Enumerable.Range(0, 20).Select(_ => entry.Amount.Resolve()).ToArray();
        Assert.All(resolves, value => Assert.InRange(value, 1, 3));
        Assert.Contains(resolves, value => value != resolves[0]);
    }

    [Fact]
    public void Run_ALootEntryReferencingAnItemDefinedInAFileScannedLater_StillResolves()
    {
        // File names deliberately sort the referencing loot block before the item that defines its
        // target, the exact ordering that broke Id resolution before every block's Id was precomputed
        // up front instead of being filled in as each file happened to be visited. The output path
        // no longer depends on the source file's own name (each loot table gets its own file, named
        // after its own Id), only the resolution itself is what this test is pinning down.
        _dirs.WriteSource(
            "aaa_lootlist.dfn",
            """
            [LOOTLIST eartheleLoot]
            {
            10|0x0f0f
            }
            """
        );
        _dirs.WriteSource(
            "zzz_items.dfn",
            """
            [0x0f0f]
            {
            id=0x0f0f
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<LootTemplateFile>(
            Path.Combine(_dirs.LootDestinationDirectory, "earthele_loot.toml")
        );
        var entry = Assert.Single(Assert.Single(file!.Loot).Entries);
        Assert.Equal("0x0f0f", entry.ItemId);
    }

    [Fact]
    public void Run_WithoutLootDestination_LeavesLootListBlocksUnconverted()
    {
        _dirs.WriteSource(
            "loot.dfn",
            """
            [0x0f0f]
            {
            id=0x0f0f
            }

            [LOOTLIST eartheleLoot]
            {
            10|0x0f0f
            }
            """
        );

        var exitCode = Run(false);

        Assert.True(exitCode == 0, CombinedOutput);
        var file = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "loot.toml"));
        Assert.Single(file!.Item);
        Assert.False(Directory.Exists(_dirs.LootDestinationDirectory));
    }

    [Fact]
    public void Run_ASuccessfulConversion_VerifiesTheOutputReadBackFromDisk()
    {
        _dirs.WriteSource(
            "items.dfn",
            """
            [base_torch]
            {
            id=0x0f6b
            }

            [LOOTLIST eartheleLoot]
            {
            10|base_torch
            }
            """
        );

        var exitCode = Run();

        Assert.True(exitCode == 0, CombinedOutput);
        Assert.Contains(
            "Verified 1 item(s) and 1 loot table(s) read back from disk",
            CombinedOutput,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Run_TwoHeadersCollidingOnlyAfterSnakeCase_FailsVerificationWithANonZeroExitCode()
    {
        // "Base-Item" and "base_item" are two distinct headers - neither duplicate-header check
        // above skips either - but ToSnakeCase collapses both to the same final Id, which only a
        // real read-back of what was written can catch.
        _dirs.WriteSource(
            "items.dfn",
            """
            [Base-Item]
            {
            id=0x0f6b
            }

            [base_item]
            {
            id=0x0f6c
            }
            """
        );

        var exitCode = Run();

        Assert.NotEqual(0, exitCode);
        Assert.Contains(
            "Verification failed: item 'base_item' is defined more than once",
            CombinedOutput,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Run_AnIdListOrATypoedId_KeepsTheFirstGraphic()
    {
        // UOX3 picks one id of a list at random (items.cpp); real data also has id=0x0x04FC and id=0x15b6].
        _dirs.WriteSource(
            "items.dfn",
            "[base_item]\n{\nid=0x0000\n}\n[cotton]\n{\nget=base_item\nid=0x0c4f 0x0c50\n}\n" +
            "[0x04fc]\n{\nid=0x0x04FC\n}\n[0x15b6]\n{\nid=0x15b6]\n}\n"
        );

        Assert.True(Run() == 0, CombinedOutput);

        var items = ReadItems();
        Assert.Equal((0x0C4Fu, "base_item"), (items["cotton"].ItemId.Value, items["cotton"].BaseId));
        Assert.Equal(0x04FCu, items["0x04fc"].ItemId.Value);
        Assert.Equal(0x15B6u, items["0x15b6"].ItemId.Value);
    }

    [Fact]
    public void Run_ATagWithNoValue_IsIgnored_AsUox3Does()
    {
        _dirs.WriteSource("items.dfn", "[base_item]\n{\nid=0x0000\ndecay=\npileable=\n}\n");

        Assert.True(Run() == 0, CombinedOutput);

        var item = ReadItems()["base_item"];
        Assert.Null(item.Decays);
        Assert.Null(item.Stackable);
    }

    [Fact]
    public void Run_ColourIsReadLikeColor_AndAnItemWithNoColourWritesNoHue()
    {
        _dirs.WriteSource(
            "items.dfn",
            "[0x0001]\n{\nid=0x0001\ncolour=0x44E\n}\n[0x0002]\n{\nget=0x0001\nid=0x0002\n}\n"
        );

        Assert.True(Run() == 0, CombinedOutput);

        var items = ReadItems();
        Assert.Equal("0x044E", items["0x0001"].Hue.ToString());
        // No colour of its own: the server's loader takes the parent's.
        Assert.Null(items["0x0002"].Hue);
    }

    [Fact]
    public void Run_NumbersMayBeHex_AsUox3Reads()
    {
        _dirs.WriteSource("items.dfn", "[ring]\n{\nid=0x108a\nlayer=0x08\nweight=0x64\n}\n");

        Assert.True(Run() == 0, CombinedOutput);

        var ring = ReadItems()["ring"];
        Assert.Equal(((LayerType?)LayerType.Ring, (decimal?)1.0m), (ring.Layer, ring.Weight));
    }

    [Fact]
    public void Run_TwoHandedLayer_MarksWeaponsButNotShieldsOrLights()
    {
        // As UOX3: layer 2 is both hands unless the item is a shield (type=107, here from a base without id=) or a
        // light (dir=), which go in the other hand.
        _dirs.WriteSource(
            "items.dfn",
            "[base_shield]\n{\ntype=107\nlayer=0x02\n}\n" +
            "[heater]\n{\nget=base_shield\nid=0x1b76\n}\n" +
            "[halberd]\n{\nid=0x143e\nlayer=2\n}\n" +
            "[torch]\n{\nid=0x0f6b\nlayer=2\ndir=14\n}\n" +
            "[katana]\n{\nid=0x13ff\nlayer=1\n}\n"
        );

        Assert.True(Run() == 0, CombinedOutput);

        var items = ReadItems();
        Assert.True(items["halberd"].TwoHandedWeapon);
        Assert.Null(items["heater"].TwoHandedWeapon);
        Assert.Null(items["torch"].TwoHandedWeapon);
        Assert.Null(items["katana"].TwoHandedWeapon);
    }

    [Fact]
    public void Run_AHeaderDefinedTwice_KeepsTheLastDefinition_AsUox3Does()
    {
        _dirs.WriteSource(
            "swords.dfn",
            "[0x2d29_lbr]\n{\nid=0x2d29\nname=machete\n}\n[0x2d29_lbr]\n{\nid=0x2d29\nname=radiant scimitar\n}\n"
        );

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal("radiant scimitar", Assert.Single(ReadItems("swords.toml").Values).Name);
    }

    [Fact]
    public void Run_Visible0_IsVisibleToEveryone_OverridingAHiddenParent()
    {
        _dirs.WriteSource("items.dfn", "[base_spawner]\n{\nid=0x1f14\nvisible=1\n}\n[d_woodbox_1]\n{\nget=base_spawner\nid=0x09aa\nvisible=0\n}\n");

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal((AccountType?)AccountType.Regular, ReadItems()["d_woodbox_1"].Visibility);
    }

    [Fact]
    public void Run_TheNecromancerSleevesAndLeggings_GetTheirRealGraphics()
    {
        // UOX3's leather.dfn has necro_sleeves get=0x13c6 (gloves) and necro_leggings get=0x13cc (a tunic).
        _dirs.WriteSource(
            "leather.dfn",
            "[0x13c6]\n{\nid=0x13c6\n}\n[0x13cc]\n{\nid=0x13cc\n}\n[0x13cd]\n{\nid=0x13cd\n}\n[0x13cb]\n{\nid=0x13cb\n}\n" +
            "[necro_sleeves]\n{\nget=0x13c6\ncolor=0x2C3\n}\n[necro_leggings]\n{\nget=0x13cc\ncolor=0x2C3\n}\n"
        );

        Assert.True(Run() == 0, CombinedOutput);

        var items = ReadItems("leather.toml");
        Assert.Equal(("0x13cd", "0x13cb"), (items["necro_sleeves"].BaseId, items["necro_leggings"].BaseId));
    }

    [Fact]
    public void Run_BadOptionsOrSource_ExitWith2()
    {
        Assert.Equal(2, UoxItemConverterCommand.Run(_dirs.SourceDirectory, _dirs.DestinationDirectory, null, _output, _error, mobileSource: "npc"));
        Assert.Contains("go together", _error.ToString());
        Assert.Equal(2, UoxItemConverterCommand.Run(Path.Combine(_dirs.SourceDirectory, "missing"), _dirs.DestinationDirectory, null, _output, _error));
        Assert.Contains("Source does not exist", _error.ToString());
        Assert.Equal(2, UoxItemConverterCommand.Run(_dirs.SourceDirectory, _dirs.DestinationDirectory, null, _output, _error));
        Assert.Contains("No .dfn files", _error.ToString());
    }

    private Dictionary<string, ItemTemplate> ReadItems(string file = "items.toml")
    {
        return TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, file))!
                        .Item.ToDictionary(item => item.Id);
    }

    [Fact]
    public void Run_WithTheScriptsSource_GivesTheItemsOfAScriptWithALuaEquivalentItsScriptId()
    {
        _dirs.WriteSource(
            "lighting.dfn",
            """
            [0x0a28]
            {
            name=candle
            id=0x0a28
            }

            [special_lamp]
            {
            id=0x0f00
            script=500
            }

            [0x0675]
            {
            name=metal door
            id=0x0675
            }

            [0x0eed]
            {
            name=gold coin
            id=0x0eed
            }
            """
        );
        _dirs.WriteScriptsSource(
            "jse_fileassociations.scp",
            """
            // comment
            [SCRIPT_LIST]
            {
            500=item/lights.js
            4500=item/doors.js
            }
            """
        );
        _dirs.WriteScriptsSource(
            "jse_objectassociations.scp",
            """
            [ENVOKE]
            {
            //Lights
            0x0a28=500
            0x0675=4500
            }
            """
        );

        var exitCode = Run(scriptsSource: _dirs.ScriptsSourceDirectory);

        Assert.True(exitCode == 0, CombinedOutput);
        var items = TomlUtils.DeserializeFromFile<ItemTemplateFile>(Path.Combine(_dirs.DestinationDirectory, "lighting.toml"))!
                             .Item.ToDictionary(item => item.Id, item => item.ScriptId);
        Assert.Equal("light", items["0x0a28_candle"]);
        Assert.Equal("light", items["special_lamp"]);
        Assert.True(string.IsNullOrEmpty(items["0x0675_metal_door"]));
        Assert.True(string.IsNullOrEmpty(items["0x0eed_gold_coin"]));
    }

    [Fact]
    public void Run_AScriptsSourceWithoutTheAssociations_Fails()
    {
        _dirs.WriteSource("items.dfn", "[coin]\n{\nid=0x0eed\n}\n");
        Directory.CreateDirectory(_dirs.ScriptsSourceDirectory);

        var exitCode = Run(scriptsSource: _dirs.ScriptsSourceDirectory);

        Assert.Equal(2, exitCode);
        Assert.Contains("jse_fileassociations.scp", _error.ToString());
    }

    private int Run(bool includeLootDestination = true, string? scriptsSource = null)
    {
        return UoxItemConverterCommand.Run(
            _dirs.SourceDirectory,
            _dirs.DestinationDirectory,
            includeLootDestination ? _dirs.LootDestinationDirectory : null,
            _output,
            _error,
            scriptsSource: scriptsSource
        );
    }

    public void Dispose()
    {
        _dirs.Dispose();
        _output.Dispose();
        _error.Dispose();
    }
}
