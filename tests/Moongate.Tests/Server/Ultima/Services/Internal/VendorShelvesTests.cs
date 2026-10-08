using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Templates.Shops;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Tests.Server.Ultima.Services.Internal;

public sealed class VendorShelvesTests
{
    private static readonly Serial Vendor = new(100);
    private static readonly DateTime Start = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly VendorShelves _shelves = new();

    [Theory,
     // Sold out: twice the maximum, up to 999.
     InlineData(100, 0, 200),
     InlineData(600, 0, 999),
     // Half or less sold (half or more left): the maximum is halved; more than half sold: it is kept.
     InlineData(100, 90, 50),
     InlineData(100, 50, 50),
     InlineData(100, 49, 100),
     InlineData(100, 20, 100),
     // A maximum of 20 or less is never halved.
     InlineData(20, 19, 20),
     // 999 is halved to 640.
     InlineData(999, 900, 640)]
    public void Restock_FillsTheShelf_ByTheRulesOfModernUo(int max, int left, int expected)
    {
        var line = new ShopLine { Item = "bread", Price = 1, Amount = max };
        var stock = _shelves.Of(Vendor, line);
        stock.Current = left;
        _shelves.Restock(Vendor, Start, [line]);

        _shelves.Restock(Vendor, Start + TimeSpan.FromMinutes(61), [line]);

        Assert.Equal(expected, stock.Current);
        Assert.Equal(expected, stock.Max);
    }

    [Fact]
    public void Restock_TheFirstTimeAVendorIsSeen_OnlyStartsTheClock_AndWithinTheHourDoesNothing()
    {
        var line = new ShopLine { Item = "bread", Price = 1, Amount = 20 };
        var stock = _shelves.Of(Vendor, line);
        stock.Current = 0;

        _shelves.Restock(Vendor, Start, [line]);
        _shelves.Restock(Vendor, Start + TimeSpan.FromMinutes(60), [line]);

        Assert.Equal(0, stock.Current);
    }

    [Fact]
    public void AddResale_JoinsTheSameGoods_AndForgetsThemAfterAnHour_OrWhenSoldOut()
    {
        var line = new ShopLine { Item = "bread", Price = 5, Amount = 2, Name = "bread" };

        _shelves.AddResale(Vendor, line, 2, Start);
        _shelves.AddResale(Vendor, line, 3, Start + TimeSpan.FromMinutes(30));

        Assert.Equal(5, Assert.Single(_shelves.ResaleOf(Vendor, Start + TimeSpan.FromMinutes(31))).Stock.Current);
        // Joining again renewed the hour.
        Assert.Single(_shelves.ResaleOf(Vendor, Start + TimeSpan.FromMinutes(89)));
        Assert.Empty(_shelves.ResaleOf(Vendor, Start + TimeSpan.FromMinutes(91)));

        _shelves.AddResale(Vendor, line, 1, Start + TimeSpan.FromMinutes(100));
        _shelves.ResaleOf(Vendor, Start + TimeSpan.FromMinutes(101))[0].Stock.Current = 0;

        Assert.Empty(_shelves.ResaleOf(Vendor, Start + TimeSpan.FromMinutes(102)));
    }
}
