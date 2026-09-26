using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Accounts;
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
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        var item = Assert.Single(file!.Item);
        Assert.Equal("base_torch", item.Id);
        Assert.Equal((uint)0x0f6b, item.ItemId.Value);
        Assert.Equal("torch", item.Name);
        Assert.True(item.Movable);
        Assert.Equal(new Hue(0x0010), item.Hue.Resolve());
        Assert.Equal(10, item.MaxWeight);
        Assert.Null(item.BaseId);
    }

    [Theory,
     InlineData("", null),
     InlineData("visible=0", null),
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
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
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
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        var wallTorch = file!.Item.Single(item => item.Id == "wall_torch");
        Assert.Equal("base_torch", wallTorch.BaseId);
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
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
        var item = Assert.Single(file!.Item);
        Assert.Equal("0x1441", item.Id);
        Assert.Null(item.BaseId);
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
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
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
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
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
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(Path.Combine(_dirs.DestinationDirectory, "items.toml"));
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
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(Path.Combine(_dirs.DestinationDirectory, "doors.toml"));
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
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
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
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
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
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
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
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
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
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
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
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
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
        var file = TomlUtils.DeserializeFromFile<ConvertedLootFile>(
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
        var file = TomlUtils.DeserializeFromFile<ConvertedItemFile>(Path.Combine(_dirs.DestinationDirectory, "loot.toml"));
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

    private int Run(bool includeLootDestination = true)
    {
        return UoxItemConverterCommand.Run(
            _dirs.SourceDirectory,
            _dirs.DestinationDirectory,
            includeLootDestination ? _dirs.LootDestinationDirectory : null,
            _output,
            _error
        );
    }

    public void Dispose()
    {
        _dirs.Dispose();
        _output.Dispose();
        _error.Dispose();
    }
}
