using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Templates.Shops;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ShopServiceTests
{
    private readonly ShopService _shops = new(
        new StubDataLoaderService().With(
            new ShopDefinition { Id = "baker", Vendors = ["baker", "f_baker"] },
            new ShopDefinition { Id = "smith", Vendors = ["blacksmith"] }
        )
    );

    [Fact]
    public void TryGetFor_ATemplateOfAShop_GivesTheShop()
    {
        Assert.True(_shops.TryGetFor("f_baker", out var shop));
        Assert.Equal("baker", shop.Id);
    }

    [Fact]
    public void TryGetFor_ATemplateWithNoShop_GivesNothing()
    {
        Assert.False(_shops.TryGetFor("orc", out var shop));
        Assert.Null(shop);
    }

    [Fact]
    public void TryGetFor_AVendorEntity_UsesItsTemplate()
    {
        Assert.True(_shops.TryGetFor(new MobileEntity { Id = new Serial(900), TemplateId = "blacksmith" }, out var shop));
        Assert.Equal("smith", shop.Id);
        Assert.False(_shops.TryGetFor(new MobileEntity { Id = new Serial(901) }, out _));
    }
}
