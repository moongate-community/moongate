using Moongate.Core.Directories;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class BannedNamesLoaderTests
{
    [Fact]
    public async Task InitializeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root).InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsBothListsAsOneEntityAndTrimsWords()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/banned_names.toml", "starts_with = [\"gm\", \" lord \"]\nwords = [\"mage\"]\n");

        var result = await CreateLoader(root).LoadDataAsync();

        var bannedNames = Assert.Single(result.Entities);
        Assert.Equal(["gm", "lord"], bannedNames.StartsWith);
        Assert.Equal(["mage"], bannedNames.Words);
    }

    [Fact]
    public async Task LoadDataAsync_EmptyFile_LoadsNoBannedWords()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/banned_names.toml", "# nothing banned\n");

        var bannedNames = Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);

        Assert.Empty(bannedNames.StartsWith);
        Assert.Empty(bannedNames.Words);
    }

    [Theory, InlineData("starts_with = [\"\"]\n"), InlineData("words = [\"mage\", \"  \"]\n")]
    public async Task LoadDataAsync_EmptyWord_ThrowsInvalidDataException(string toml)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/banned_names.toml", toml);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    private static BannedNamesLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]));
    }
}
