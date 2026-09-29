using Moongate.Core.Directories;
using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class StartingItemsLoaderTests
{
    public StartingItemsLoaderTests()
    {
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
    }

    [Fact]
    public async Task InitializeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root).InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsEverySet()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "data/starting_items.toml",
            "[[set]]\ncommon = true\n[[set.items]]\nitems = [\"dagger\"]\n\n" +
            "[[set]]\nskill = \"alchemy\"\n[[set.items]]\nitems = [\"robe\"]\namount = \"1d3\"\nequip = true\n"
        );

        var sets = (await CreateLoader(root).LoadDataAsync()).Entities;

        Assert.Equal(2, sets.Count);
        Assert.True(sets[0].Common);
        Assert.Equal((SkillType?)SkillType.Alchemy, sets[1].Skill);
    }

    [Theory,
     InlineData("[[set]]\ncommon = true\n"),
     InlineData("[[set]]\ncommon = true\n[[set.items]]\nitems = [\"cape\"]\n"),
     InlineData("[[set]]\ncommon = true\n[[set.items]]\nitems = [\"dagger\"]\namount = \"1d3-3\"\n"),
     InlineData("[[set]]\ncommon = true\n[[set.items]]\nitems = [\"dagger\"]\namount = 65536\n"),
     InlineData("[[set]]\ncommon = true\n[[set.items]]\nitems = [\"dagger\"]\namount = \"65000+1d1000\"\n"),
     InlineData("[[set]]\n[[set.items]]\nitems = [\"dagger\"]\n"),
     InlineData("[[set]]\ncommon = true\n[[set.items]]\nitems = []\n")]
    public async Task LoadDataAsync_ABadSet_ThrowsInvalidDataException(string toml)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/starting_items.toml", toml);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    private static StartingItemsLoader CreateLoader(TemporaryDirectory root)
    {
        return new(
            new DirectoriesConfig(root.Path, ["data"]),
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "dagger", ItemId = new Serial(0x0F52) },
                new ItemTemplate { Id = "robe", ItemId = new Serial(0x1F03) }
            )
        );
    }
}
