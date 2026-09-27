using Moongate.Core.Directories;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class NamesLoaderTests
{
    [Fact]
    public async Task InitializeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root).InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsEveryListAndTrimsNames()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/names.toml", "[[names]]\nid = \"male\"\nnames = [\" Aaron \", \"Abbot\"]\n\n[[names]]\nid = \"orc\"\nnames = [\"Abghat\"]\n");

        var lists = (await CreateLoader(root).LoadDataAsync()).Entities;

        Assert.Equal(["male", "orc"], lists.Select(list => list.Id));
        Assert.Equal(["Aaron", "Abbot"], lists[0].Names);
    }

    [Theory,
     InlineData("[[names]]\nid = \"\"\nnames = [\"A\"]\n"),
     InlineData("[[names]]\nid = \"male\"\nnames = []\n"),
     InlineData("[[names]]\nid = \"male\"\nnames = [\" \"]\n"),
     InlineData("[[names]]\nid = \"male\"\nnames = [\"A\"]\n[[names]]\nid = \"MALE\"\nnames = [\"B\"]\n")]
    public async Task LoadDataAsync_ABadList_ThrowsInvalidDataException(string toml)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/names.toml", toml);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    private static NamesLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]));
    }
}
