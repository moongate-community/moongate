using Moongate.Core.Directories;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class TitlesLoaderTests
{
    [Fact]
    public async Task CompleteGrid_LoadsTitlesIncludingAnEmptyPrefix()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/titles.toml", """
            [[titles]]
            fame = 0
            karma = -100
            title = "The Outcast"

            [[titles]]
            fame = 0
            karma = 0
            title = ""

            [[titles]]
            fame = 1000
            karma = -100
            title = "The Dread Lord"
            female_title = "The Dread Lady"

            [[titles]]
            fame = 1000
            karma = 0
            title = "The Glorious Lord"
            female_title = "The Glorious Lady"
            """);

        var rows = (await CreateLoader(root).LoadDataAsync()).Entities;

        Assert.Equal(4, rows.Count);
        Assert.Contains(rows, row => row.Fame == 0 && row.Karma == 0 && row.Title == "");
        Assert.Contains(rows, row => row.Fame == 1000 && row.Karma == 0 && row.FemaleTitle == "The Glorious Lady");
    }

    [Fact]
    public async Task MissingFile_FailsWithPath()
    {
        using var root = new TemporaryDirectory();

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("titles.toml", error.Message);
    }

    [Theory]
    [InlineData("[[titles]\nfame = 0\nkarma = 0\ntitle = \"unterminated\n", "invalid")]
    [InlineData("# empty\n", "titles")]
    [InlineData("[[titles]]\nfame = 0\nkarma = 0\n", "title")]
    [InlineData("[[titles]]\nfame = 0\nkarma = 0\ntitle = \" \"\n", "title")]
    public async Task InvalidFile_FailsWithPathAndReason(string toml, string reason)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/titles.toml", toml);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("titles.toml", error.Message);
        Assert.Contains(reason, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicatePair_FailsWithPair()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/titles.toml", """
            [[titles]]
            fame = 0
            karma = -100
            title = "A"

            [[titles]]
            fame = 0
            karma = -100
            title = "B"
            """);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("titles.toml", error.Message);
        Assert.Contains("0", error.Message);
        Assert.Contains("-100", error.Message);
    }

    [Fact]
    public async Task MissingGridPair_FailsWithPair()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/titles.toml", """
            [[titles]]
            fame = 0
            karma = -100
            title = "A"

            [[titles]]
            fame = 0
            karma = 0
            title = "B"

            [[titles]]
            fame = 1000
            karma = -100
            title = "C"
            """);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("titles.toml", error.Message);
        Assert.Contains("1000", error.Message);
        Assert.Contains("0", error.Message);
    }

    private static TitlesLoader CreateLoader(TemporaryDirectory root)
    {
        return new TitlesLoader(new DirectoriesConfig(root.Path, ["data"]));
    }
}
