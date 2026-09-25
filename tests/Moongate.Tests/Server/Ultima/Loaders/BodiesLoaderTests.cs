using Moongate.Core.Directories;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class BodiesLoaderTests
{
    [Fact]
    public async Task InitializeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root).InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_IdsAndRanges_BecomeOneEntryPerBodySortedById()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "data/bodies.toml",
            "human = [\"400-401\"]\nanimal = [\"5\"]\nmonster = [\"1-2\"]\nsea = [\"150\"]\nequipment = [\"1000\"]\n"
        );

        var result = await CreateLoader(root).LoadDataAsync();

        Assert.Equal(
            [
                (1, BodyType.Monster), (2, BodyType.Monster), (5, BodyType.Animal), (150, BodyType.Sea),
                (400, BodyType.Human), (401, BodyType.Human), (1000, BodyType.Equipment)
            ],
            result.Entities.Select(body => ((int)body.Body.Value, body.Type))
        );
    }

    [Fact]
    public async Task LoadDataAsync_KindLeftOut_IsEmpty()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/bodies.toml", "human = [\"400\"]\n");

        var body = Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);

        Assert.Equal(new Body(400), body.Body);
    }

    [Fact]
    public async Task LoadDataAsync_BodyUnderTwoKinds_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/bodies.toml", "human = [\"400-402\"]\nmonster = [\"402\"]\n");

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("402", exception.Message, StringComparison.Ordinal);
    }

    [Theory,
     InlineData("\"10-5\""),
     InlineData("\"a\""),
     InlineData("\"1-2-3\""),
     InlineData("\"-5\""),
     InlineData("\"70000\""),
     InlineData("\"\"")]
    public async Task LoadDataAsync_InvalidEntry_ThrowsInvalidDataException(string entry)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/bodies.toml", $"monster = [{entry}]\n");

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    private static BodiesLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]));
    }
}
