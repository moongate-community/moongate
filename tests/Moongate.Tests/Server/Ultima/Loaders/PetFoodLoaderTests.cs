using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class PetFoodLoaderTests
{
    private const string Fruit = "[[food]]\nkind = \"fruit\"\nitems = [\"apple\", \"pear\"]\n";

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsTheKinds()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/pet_food.toml", Fruit + "[[food]]\nkind = \"meat\"\nitems = [\"apple\"]\n");

        var foods = (await CreateLoader(root).LoadDataAsync()).Entities.ToArray();

        Assert.Equal([("fruit", 2), ("meat", 1)], foods.Select(food => (food.Kind, food.Items.Count)));
    }

    [Fact]
    public async Task LoadDataAsync_NoFile_LoadsNothing()
    {
        using var root = new TemporaryDirectory();

        Assert.Empty((await CreateLoader(root).LoadDataAsync()).Entities);
    }

    [Theory,
     InlineData("kind = \"cheese\"\nitems = []", "cheese"),
     InlineData("kind = \"fruit\"\nitems = [\"nothing\"]", "nothing")]
    public async Task LoadDataAsync_ABadKind_StopsTheServerNamingIt(string body, string mentions)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/pet_food.toml", "[[food]]\n" + body + "\n");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains(mentions, error.Message);
    }

    [Fact]
    public async Task LoadDataAsync_AKindTwice_StopsTheServer()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/pet_food.toml", Fruit + Fruit);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("twice", error.Message);
    }

    private static PetFoodLoader CreateLoader(TemporaryDirectory root)
    {
        return new(
            new DirectoriesConfig(root.Path, ["data"]),
            new StubDataLoaderService().With(new ItemTemplate { Id = "apple" }, new ItemTemplate { Id = "pear" })
        );
    }
}
