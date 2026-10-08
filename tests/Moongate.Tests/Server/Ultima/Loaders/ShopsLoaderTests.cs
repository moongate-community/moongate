using Moongate.Core.Directories;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class ShopsLoaderTests
{
    private const string Good =
        "[[shop]]\nid = \"baker\"\nvendors = [\"baker\"]\n[[shop.buy]]\nitem = \"bread\"\nprice = 8\namount = 20\nhue = 5\nname = \"Loaf\"\n";

    [Fact]
    public async Task LoadDataAsync_ReadsTheShopsOfEveryFile()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/shops/sub/a.toml", Good);

        var shop = Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);
        var line = Assert.Single(shop.Buy);

        Assert.Equal(("baker", "baker"), (shop.Id, Assert.Single(shop.Vendors)));
        Assert.Equal(("bread", 8, 20, 5, "Loaf"), (line.Item, line.Price, line.Amount, line.Hue, line.Name));
    }

    [Fact]
    public async Task LoadDataAsync_WithNoFolder_GivesNoShops()
    {
        using var root = new TemporaryDirectory();

        Assert.Empty((await CreateLoader(root).LoadDataAsync()).Entities);
    }

    [Theory,
     InlineData("unknown item", "[[shop]]\nid = \"a\"\n[[shop.buy]]\nitem = \"cape\"\nprice = 1\n"),
     InlineData("price", "[[shop]]\nid = \"a\"\n[[shop.buy]]\nitem = \"bread\"\nprice = 0\n"),
     InlineData("amount", "[[shop]]\nid = \"a\"\n[[shop.buy]]\nitem = \"bread\"\nprice = 1\namount = 0\n"),
     InlineData("unknown vendor", "[[shop]]\nid = \"a\"\nvendors = [\"ghost\"]\n"),
     InlineData(
         "in two shops",
         "[[shop]]\nid = \"a\"\nvendors = [\"baker\"]\n[[shop]]\nid = \"b\"\nvendors = [\"baker\"]\n"
     ),
     InlineData("duplicate shop", "[[shop]]\nid = \"a\"\n[[shop]]\nid = \"a\"\n"),
     InlineData("amount too big", "[[shop]]\nid = \"a\"\n[[shop.buy]]\nitem = \"bread\"\nprice = 1\namount = 60001\n"),
     InlineData("hue", "[[shop]]\nid = \"a\"\n[[shop.buy]]\nitem = \"bread\"\nprice = 1\nhue = 70000\n"),
     InlineData("name", "[[shop]]\nid = \"a\"\n[[shop.buy]]\nitem = \"bread\"\nprice = 1\nname = \"pozione è\"\n"),
     InlineData(
         "same line twice",
         "[[shop]]\nid = \"a\"\n[[shop.buy]]\nitem = \"bread\"\nprice = 1\n[[shop.buy]]\nitem = \"bread\"\nprice = 1\n"
     ),
     InlineData("no id", "[[shop]]\nvendors = []\n")]
    public async Task LoadDataAsync_ABadShop_ThrowsInvalidDataException_NamingTheFault(string fault, string toml)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/shops/a.toml", toml);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("a.toml", exception.Message);
        Assert.NotEmpty(fault);
    }

    [Fact]
    public async Task LoadDataAsync_ASellLineWithAnUnknownItem_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/shops/a.toml", "[[shop]]\nid = \"a\"\n[[shop.sell]]\nitem = \"cape\"\nprice = 1\n");

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    private static ShopsLoader CreateLoader(TemporaryDirectory root)
    {
        return new(
            new DirectoriesConfig(root.Path, ["templates"]),
            new StubDataLoaderService()
                .With(new ItemTemplate { Id = "bread", ItemId = new Serial(0x103B) })
                .With(new MobileTemplate { Id = "baker" })
        );
    }
}
