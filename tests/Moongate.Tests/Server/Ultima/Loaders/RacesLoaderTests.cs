using Moongate.Core.Directories;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Ultima.Types;
using Tomlyn;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class RacesLoaderTests
{
    private const string Human = """
                                 [[race]]
                                 race = "human"
                                 name = "Human"

                                 [race.male]
                                 body = 400
                                 hair = [0x203B, 0x203C]
                                 beard = [0x203E]

                                 [race.female]
                                 body = 401
                                 hair = [0x203B, 0x2046]
                                 beard = []

                                 """;

    [Fact]
    public async Task InitializeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root).InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsBothGenders()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/races.toml", Human);

        var result = await CreateLoader(root).LoadDataAsync();

        var human = Assert.Single(result.Entities);
        Assert.Equal(RaceType.Human, human.Race);
        Assert.Equal("Human", human.Name);
        Assert.Equal(400, human.Male.Body);
        Assert.Equal([0x203B, 0x203C], human.Male.Hair);
        Assert.Equal([0x203E], human.Male.Beard);
        Assert.Equal(401, human.For(GenderType.Female).Body);
        Assert.Empty(human.For(GenderType.Female).Beard);
    }

    [Fact]
    public async Task LoadDataAsync_RaceListedTwice_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/races.toml", Human + Human);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_MissingGenderSection_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        var withoutFemale = Human[..Human.IndexOf("[race.female]", StringComparison.Ordinal)];
        root.CreateFile("data/races.toml", withoutFemale);

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("female", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadDataAsync_MissingBody_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/races.toml", Human.Replace("body = 400\n", string.Empty));

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Theory, InlineData("[0x203B, 0x203C]", "[0x203B, 0]"), InlineData("[0x203E]", "[0x10000]")]
    public async Task LoadDataAsync_StyleOutsideTheItemIdRange_ThrowsInvalidDataException(string from, string to)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/races.toml", Human.Replace(from, to));

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_UnknownRace_ThrowsTomlException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/races.toml", Human.Replace("\"human\"", "\"orc\""));

        await Assert.ThrowsAsync<TomlException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_TableWithAnotherName_ThrowsInsteadOfLoadingNothing()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "data/races.toml",
            Human.Replace("[[race]]", "[[races]]").Replace("[race.", "[races.")
        );

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    private static RacesLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]));
    }
}
