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

    public void Dispose()
    {
        _converter.Dispose();
    }
}
