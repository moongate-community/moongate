using Moongate.Core.Directories;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class SkillsLoaderTests
{
    private const string Alchemy = """
                                   [[skill]]
                                   id = "alchemy"
                                   name = "Alchemy"
                                   title = "Alchemist"
                                   profession_name = "Alchemy"
                                   primary_stat = "int"
                                   secondary_stat = "dex"
                                   str_scale = 0.0
                                   dex_scale = 5.0
                                   int_scale = 5.0
                                   str_gain = 0.0
                                   dex_gain = 0.5
                                   int_gain = 0.5
                                   gain_factor = 1.0

                                   """;

    private const string Anatomy = """
                                   [[skill]]
                                   id = "anatomy"
                                   name = "Anatomy"
                                   title = "Biologist"
                                   profession_name = "Anatomy"
                                   primary_stat = "str"
                                   secondary_stat = "int"
                                   str_scale = 0.0
                                   dex_scale = 0.0
                                   int_scale = 0.0
                                   str_gain = 0.15
                                   dex_gain = 0.15
                                   int_gain = 0.7
                                   gain_factor = 1.0

                                   """;

    [Fact]
    public async Task InitializeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();
        var loader = CreateLoader(root);

        await Assert.ThrowsAsync<FileNotFoundException>(() => loader.InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsEverySkillInOrder()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/skills.toml", Alchemy + Anatomy);
        var loader = CreateLoader(root);

        await loader.InitializeAsync();
        var result = await loader.LoadDataAsync();

        Assert.Equal(2, result.Entities.Count);
        var alchemy = result.Entities[0];
        Assert.Equal(SkillType.Alchemy, alchemy.Id);
        Assert.Equal("Alchemy", alchemy.Name);
        Assert.Equal("Alchemist", alchemy.Title);
        Assert.Equal("Alchemy", alchemy.ProfessionName);
        Assert.Equal(StatType.Int, alchemy.PrimaryStat);
        Assert.Equal(StatType.Dex, alchemy.SecondaryStat);
        Assert.Equal(5.0, alchemy.DexScale);
        Assert.Equal(5.0, alchemy.IntScale);
        Assert.Equal(10.0, alchemy.StatTotal);
        Assert.Equal(0.5, alchemy.IntGain);
        Assert.Equal(1.0, alchemy.GainFactor);
        Assert.Equal(StatType.Str, result.Entities[1].PrimaryStat);
    }

    [Fact]
    public async Task LoadDataAsync_IdsOutOfOrder_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/skills.toml", Anatomy + Alchemy);
        var loader = CreateLoader(root);

        await Assert.ThrowsAsync<InvalidDataException>(() => loader.LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_IdGap_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/skills.toml", Anatomy);
        var loader = CreateLoader(root);

        await Assert.ThrowsAsync<InvalidDataException>(() => loader.LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_TableWithAnotherName_ThrowsInsteadOfLoadingNothing()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/skills.toml", Alchemy.Replace("[[skill]]", "[[skills]]"));
        var loader = CreateLoader(root);

        await Assert.ThrowsAsync<InvalidDataException>(() => loader.LoadDataAsync());
    }

    private static SkillsLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]));
    }
}
