using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Tests.TestSupport.Scripts;

namespace Moongate.Tests.Integration.Scripts;

public sealed class UoxItemConverterTests : IDisposable
{
    private readonly ScriptedUoxItemConverter _converter = new();

    public UoxItemConverterTests()
    {
        AppContext.SetSwitch("Tomlyn.TomlSerializer.IsReflectionEnabledByDefault", true);
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
    }

    [Fact]
    public async Task Run_ABlockWithItsOwnFields_ConvertsEveryMappedField()
    {
        _converter.WriteSource(
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

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(
            Path.Combine(_converter.DestinationDirectory, "items.toml")
        );
        var item = Assert.Single(file!.Item);
        Assert.Equal("base_torch", item.Id);
        Assert.Equal((uint)0x0f6b, item.ItemId.Value);
        Assert.Equal("torch", item.Name);
        Assert.True(item.Movable);
        Assert.Equal(0x0010, item.Hue.Resolve());
        Assert.Equal(10, item.MaxWeight);
        Assert.Null(item.BaseId);
    }

    [Fact]
    public async Task Run_ABlockWithASingleGetTarget_ResolvesBaseIdAgainstTheConvertedParent()
    {
        _converter.WriteSource(
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

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(
            Path.Combine(_converter.DestinationDirectory, "items.toml")
        );
        var wallTorch = file!.Item.Single(item => item.Id == "wall_torch");
        Assert.Equal("base_torch", wallTorch.BaseId);
    }

    [Fact]
    public async Task Run_AGetTargetThatNeverConverted_LeavesBaseIdUnset()
    {
        _converter.WriteSource(
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

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(
            Path.Combine(_converter.DestinationDirectory, "items.toml")
        );
        var item = Assert.Single(file!.Item);
        Assert.Equal("0x1441", item.Id);
        Assert.Null(item.BaseId);
    }

    [Fact]
    public async Task Run_ABlockWithMultipleGetTargetsAndNoIdOfItsOwn_IsSkipped()
    {
        _converter.WriteSource(
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

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(
            Path.Combine(_converter.DestinationDirectory, "items.toml")
        );
        var item = Assert.Single(file!.Item);
        Assert.Equal("base_torch", item.Id);
    }

    [Fact]
    public async Task Run_ANameOnABareHexHeader_PrefixesTheNameWithTheHeader()
    {
        // The header, not the name, is what guarantees uniqueness: UOX3 reuses the same name=
        // across many variants of the same conceptual item (verified against real data: "wooden
        // door" 64 times, "ballista" 239 times), so name= alone would collide.
        _converter.WriteSource(
            "items.dfn",
            """
            [0x1441]
            {
            name=cutlass_ns
            id=0x1441
            }
            """
        );

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(
            Path.Combine(_converter.DestinationDirectory, "items.toml")
        );
        var item = Assert.Single(file!.Item);
        Assert.Equal("0x1441_cutlass_ns", item.Id);
    }

    [Fact]
    public async Task Run_ANameWithSpacesOnABareHexHeader_ProducesASnakeCaseId()
    {
        // Real name= values are free text ("pitcher of wine", "bone gloves"): the combined Id goes
        // through ToSnakeCase so it never carries a literal space.
        _converter.WriteSource(
            "items.dfn",
            """
            [0x1f9b]
            {
            name=pitcher of wine
            id=0x1f9b
            }
            """
        );

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(
            Path.Combine(_converter.DestinationDirectory, "items.toml")
        );
        var item = Assert.Single(file!.Item);
        Assert.Equal("0x1f9b_pitcher_of_wine", item.Id);
    }

    [Fact]
    public async Task Run_TheSameNameOnDifferentBareHexHeaders_ProducesDistinctIds()
    {
        _converter.WriteSource(
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

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(
            Path.Combine(_converter.DestinationDirectory, "doors.toml")
        );
        var ids = file!.Item.Select(item => item.Id).ToArray();
        Assert.Equal(2, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Run_ADirectoryOfSourceFiles_MirrorsTheirRelativePathsUnderDestination()
    {
        _converter.WriteSource(
            "gear/weapons/swords.dfn",
            """
            [base_katana]
            {
            id=0x13fe
            }
            """
        );

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.True(File.Exists(Path.Combine(_converter.DestinationDirectory, "gear", "weapons", "swords.toml")));
    }

    [Fact]
    public async Task Run_NoSourceOrDestination_FailsWithUsage()
    {
        var result = await _converter.RunAsync(withArguments: false);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Usage:", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_ALootListBlock_ConvertsWeightedEntriesAgainstItemsInTheSameFile()
    {
        _converter.WriteSource(
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

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
            Path.Combine(_converter.LootDestinationDirectory, "loot.toml")
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
    public async Task Run_ALootEntryWithNoWeightPrefix_DefaultsToWeightOne()
    {
        _converter.WriteSource(
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

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
            Path.Combine(_converter.LootDestinationDirectory, "loot.toml")
        );
        var entry = Assert.Single(Assert.Single(file!.Loot).Entries);
        Assert.Equal(1, entry.Weight);
    }

    [Fact]
    public async Task Run_ANestedLootListReference_ResolvesLootTemplateIdAndAmount()
    {
        _converter.WriteSource(
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

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
            Path.Combine(_converter.LootDestinationDirectory, "loot.toml")
        );
        var eartheleLoot = file!.Loot.Single(l => l.Id == "earthele_loot");
        var entry = Assert.Single(eartheleLoot.Entries);
        Assert.Equal("randomgems", entry.LootTemplateId);
        Assert.Null(entry.ItemId);
        Assert.Equal(2, entry.Amount.Resolve());
    }

    [Fact]
    public async Task Run_ALootEntryPointingAtNothingResolvable_IsSkipped()
    {
        _converter.WriteSource(
            "loot.dfn",
            """
            [LOOTLIST eartheleLoot]
            {
            10|LOOTLIST=nonexistent
            10|0x9999
            }
            """
        );

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("2 loot entry/entries", result.Output, StringComparison.Ordinal);
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
            Path.Combine(_converter.LootDestinationDirectory, "loot.toml")
        );
        Assert.Empty(Assert.Single(file!.Loot).Entries);
    }

    [Fact]
    public async Task Run_ALootEntryWithAMinMaxAmount_ParsesARangeSpec()
    {
        _converter.WriteSource(
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

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
            Path.Combine(_converter.LootDestinationDirectory, "loot.toml")
        );
        var entry = Assert.Single(Assert.Single(file!.Loot).Entries);
        var resolves = Enumerable.Range(0, 20).Select(_ => entry.Amount.Resolve()).ToArray();
        Assert.All(resolves, value => Assert.InRange(value, 1, 3));
        Assert.Contains(resolves, value => value != resolves[0]);
    }

    [Fact]
    public async Task Run_ALootEntryReferencingAnItemDefinedInAFileScannedLater_StillResolves()
    {
        // File names deliberately sort the referencing loot block before the item that defines its
        // target, the exact ordering that broke Id resolution before every block's Id was precomputed
        // up front instead of being filled in as each file happened to be visited.
        _converter.WriteSource(
            "aaa_lootlist.dfn",
            """
            [LOOTLIST eartheleLoot]
            {
            10|0x0f0f
            }
            """
        );
        _converter.WriteSource(
            "zzz_items.dfn",
            """
            [0x0f0f]
            {
            id=0x0f0f
            }
            """
        );

        var result = await _converter.RunAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
            Path.Combine(_converter.LootDestinationDirectory, "aaa_lootlist.toml")
        );
        var entry = Assert.Single(Assert.Single(file!.Loot).Entries);
        Assert.Equal("0x0f0f", entry.ItemId);
    }

    [Fact]
    public async Task Run_WithoutLootDestination_LeavesLootListBlocksUnconverted()
    {
        _converter.WriteSource(
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

        var result = await _converter.RunAsync(includeLootDestination: false);

        Assert.True(result.ExitCode == 0, result.Output);
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(
            Path.Combine(_converter.DestinationDirectory, "loot.toml")
        );
        Assert.Single(file!.Item);
        Assert.False(Directory.Exists(_converter.LootDestinationDirectory));
    }

    public void Dispose()
    {
        _converter.Dispose();
    }
}
