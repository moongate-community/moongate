using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ContainerCapacityServiceTests
{
    private static readonly Serial Owner = new(2);

    private readonly ItemService _items = TestItems.Create();
    private readonly BankConfig _config = new() { MaxItems = 5 };

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "bank_box", ItemId = new Serial(0x0E7C) },
            new ItemTemplate { Id = "small_bag", ItemId = new Serial(0x0E76), MaxItems = 3 },
            new ItemTemplate { Id = "bag", ItemId = new Serial(0x0E76) },
            new ItemTemplate { Id = "coin", ItemId = new Serial(0x0EED) }
        )
    );

    private uint _next = 0x40000100;
    private readonly ContainerCapacityService _capacity;

    public ContainerCapacityServiceTests()
    {
        _capacity = new ContainerCapacityService(_items, _templates, _config);
    }

    [Fact]
    public void AContainerWhoseTemplateSaysNothing_HasNoLimit()
    {
        var bag = OnTheGround("bag");
        Fill(bag, 300);

        Assert.Null(_capacity.MaximumOf(bag));
        Assert.True(_capacity.HasRoom(bag, Loose("coin")));
    }

    [Fact]
    public void AContainer_HoldsAsManyItemsAsItsTemplateSays()
    {
        var bag = OnTheGround("small_bag");
        Fill(bag, 2);

        Assert.Equal(3, _capacity.MaximumOf(bag));
        Assert.True(_capacity.HasRoom(bag, Loose("coin")));

        Fill(bag, 1);

        Assert.False(_capacity.HasRoom(bag, Loose("coin")));
    }

    [Fact]
    public void CountIn_CountsWhatIsInsideTheBagsToo_AndAPileAsOne()
    {
        var bag = OnTheGround("bag");
        Fill(bag, 2);
        var inner = In(bag, "bag");
        Fill(inner, 3);
        In(bag, "coin").Amount = 5000;

        // Two items, the inner bag with its three, and the pile.
        Assert.Equal(7, _capacity.CountIn(bag));
    }

    [Fact]
    public void ABagWithThingsInside_CountsWithThem()
    {
        var small = OnTheGround("small_bag");
        var carried = Loose("bag");
        Fill(carried, 2);

        // The bag and its two items: three, the limit.
        Assert.True(_capacity.HasRoom(small, carried));

        Fill(carried, 1);

        Assert.False(_capacity.HasRoom(small, carried));
    }

    [Fact]
    public void TheBankBox_TakesItsLimitFromTheSettings()
    {
        var box = BankBox();
        Fill(box, 4);

        Assert.Equal(5, _capacity.MaximumOf(box));
        Assert.True(_capacity.HasRoom(box, Loose("coin")));

        Fill(box, 1);

        Assert.False(_capacity.HasRoom(box, Loose("coin")));
    }

    [Fact]
    public void TheBankBox_WithZeroInTheSettings_HasNoLimit()
    {
        _config.MaxItems = 0;
        var box = BankBox();
        Fill(box, 500);

        Assert.Null(_capacity.MaximumOf(box));
        Assert.True(_capacity.HasRoom(box, Loose("coin")));
    }

    // A drop into a bag that is inside the bank box fills the box too.
    [Fact]
    public void ABagInsideTheBankBox_IsCheckedAgainstTheBoxToo()
    {
        var box = BankBox();
        var bag = In(box, "bag");
        Fill(box, 3);

        // The bag and three items in the box: one place left, wherever it goes.
        Assert.True(_capacity.HasRoom(bag, Loose("coin")));

        Fill(bag, 1);

        Assert.False(_capacity.HasRoom(bag, Loose("coin")));
        Assert.False(_capacity.HasRoom(box, Loose("coin")));
    }

    // Moved from one place to another of the same container: it holds no more than before.
    [Fact]
    public void AnItemAlreadyInside_CanMoveAroundAFullContainer()
    {
        var box = BankBox();
        var bag = In(box, "bag");
        Fill(box, 3);
        var coin = In(box, "coin");

        Assert.Equal(5, _capacity.CountIn(box));
        Assert.True(_capacity.HasRoom(box, coin));
        Assert.True(_capacity.HasRoom(bag, coin));
    }

    [Fact]
    public void AnItemInsideAFullSmallBag_CannotGoIntoAnotherFullOne()
    {
        var first = OnTheGround("small_bag");
        var second = OnTheGround("small_bag");
        Fill(second, 3);
        var coin = In(first, "coin");

        Assert.False(_capacity.HasRoom(second, coin));
    }

    [Theory, InlineData(1, true), InlineData(2, false), InlineData(0, true)]
    public void HasRoomFor_ANumberOfNewItems(int items, bool room)
    {
        var box = BankBox();
        Fill(box, 4);

        Assert.Equal(room, _capacity.HasRoomFor(box, items));
    }

    private ItemEntity BankBox()
    {
        var box = New("bank_box");
        box.Equip(Owner, LayerType.Bank);
        _items.Add([box]);

        return box;
    }

    private ItemEntity OnTheGround(string template)
    {
        var item = New(template);
        item.PlaceOnGround(MapType.Trammel, new Point3D(100, 100, 0));
        _items.Add([item]);

        return item;
    }

    private ItemEntity In(ItemEntity container, string template)
    {
        var item = New(template);
        item.PutInContainer(container.Id, new Point2D(50, 50), 0);
        _items.Add([item]);

        return item;
    }

    // Held on a cursor: nowhere yet.
    private ItemEntity Loose(string template)
    {
        var item = New(template);
        _items.Add([item]);

        return item;
    }

    private void Fill(ItemEntity container, int count)
    {
        for (var index = 0; index < count; index++)
        {
            In(container, "coin");
        }
    }

    private ItemEntity New(string template)
    {
        return new() { Id = new Serial(_next++), TemplateId = template, ItemId = 0x0EED, Amount = 1 };
    }
}
