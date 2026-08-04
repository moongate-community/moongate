using Moongate.Http.Plugin.Data.Api.Characters;
using Moongate.Http.Plugin.Services.Characters;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Http.Characters;

/// <summary>
/// The walk is recursive over a containment graph nothing validates, and it runs on a request thread.
/// Its guards are the point of the class: without them a bag containing an ancestor is not a wrong
/// answer, it is a hung request.
/// </summary>
public class CharacterInventoryReaderTests
{
    // The serial form the rest of the API reports, so a caller can feed it straight back.
    [Fact]
    public void Items_CarryTheSerialInTheApiForm()
    {
        var items = new StubItemService([]);
        var backpack = Container(items, 100);

        Put(items, backpack, Item(items, 0x40000001, "sword"));

        var contents = new CharacterInventoryReader(items, new ItemTemplateService(), new StubClilocService()).ReadBackpack(MobileWith(backpack));

        Assert.Equal("0x40000001", Assert.Single(contents).Serial);
    }

    [Fact]
    public void ReadBackpack_DescendsIntoNestedContainers()
    {
        var items = new StubItemService([]);
        var backpack = Container(items, 100);
        var bag = Container(items, 101, "bag");

        Put(items, backpack, bag);
        Put(items, bag, Item(items, 1, "potion"));

        var contents = new CharacterInventoryReader(items, new ItemTemplateService(), new StubClilocService()).ReadBackpack(MobileWith(backpack));

        var found = Assert.Single(contents);

        Assert.Equal("bag", found.Name);
        Assert.Equal("potion", Assert.Single(found.Contents).Name);
    }

    [Fact]
    public void ReadBackpack_IsEmptyWhenTheCharacterHasNoBackpack()
    {
        var reader = new CharacterInventoryReader(new StubItemService([]), new ItemTemplateService(), new StubClilocService());

        Assert.Empty(reader.ReadBackpack(new() { Id = new(1) }));
    }

    [Fact]
    public void ReadBackpack_ReturnsWhatIsDirectlyInside()
    {
        var items = new StubItemService([]);
        var backpack = Container(items, 100);

        Put(items, backpack, Item(items, 1, "sword"));
        Put(items, backpack, Item(items, 2, "gold"));

        var contents = new CharacterInventoryReader(items, new ItemTemplateService(), new StubClilocService()).ReadBackpack(MobileWith(backpack));

        Assert.Equal(["sword", "gold"], contents.Select(item => item.Name));
    }

    [Fact]
    public void ReadBackpack_StopsAtTheDepthLimit()
    {
        var items = new StubItemService([]);
        var backpack = Container(items, 100);
        var current = backpack;

        for (var depth = 0; depth < 25; depth++)
        {
            var bag = Container(items, (uint)(200 + depth), $"bag{depth}");

            Put(items, current, bag);
            current = bag;
        }

        var contents = new CharacterInventoryReader(items, new ItemTemplateService(), new StubClilocService()).ReadBackpack(MobileWith(backpack));

        Assert.Equal(CharacterInventoryReader.MaxDepth, Depth(contents));
    }

    // Nothing stops a bag from containing an ancestor, and the walk runs on a request thread: a cycle
    // without this guard hangs the request rather than answering wrongly.
    [Fact]
    public void ReadBackpack_SurvivesAContainerThatContainsAnAncestor()
    {
        var items = new StubItemService([]);
        var backpack = Container(items, 100);
        var bag = Container(items, 101, "bag");

        Put(items, backpack, bag);
        Put(items, bag, backpack); // the cycle

        var contents = new CharacterInventoryReader(items, new ItemTemplateService(), new StubClilocService()).ReadBackpack(MobileWith(backpack));

        // It returns rather than hanging, and stops where it would repeat itself.
        var bagNode = Assert.Single(contents);

        Assert.Equal("bag", bagNode.Name);
        Assert.Empty(Assert.Single(bagNode.Contents).Contents);
    }

    [Fact]
    public void ReadEquipment_NamesTheLayerOfEachWornItem()
    {
        var robe = Item(new([]), 5, "robe");

        robe.EquippedLayer = LayerType.OuterTorso;

        var worn = new CharacterInventoryReader(new StubItemService([robe]), new ItemTemplateService(), new StubClilocService()).ReadEquipment(new() { Id = new(1) });

        var found = Assert.Single(worn);

        Assert.Equal("robe", found.Name);
        Assert.Equal("OuterTorso", found.Layer);
    }

    private static ItemEntity Container(StubItemService items, uint serial, string name = "backpack")
        => Item(items, serial, name);

    private static int Depth(IReadOnlyList<CharacterItemResponse> nodes)
        => nodes.Count == 0 ? 0 : 1 + nodes.Max(node => Depth(node.Contents));

    private static ItemEntity Item(StubItemService items, uint serial, string name)
        => items.Track(new() { Id = new(serial), Name = name, TemplateId = name, ItemId = 0x13B9, Amount = 1 });

    private static MobileEntity MobileWith(ItemEntity backpack)
    {
        var mobile = new MobileEntity { Id = new(1), BackpackId = backpack.Id };

        mobile.EquippedItemIds[LayerType.Backpack] = backpack.Id;

        return mobile;
    }

    private static void Put(StubItemService items, ItemEntity container, ItemEntity item)
    {
        container.ContainedItemIds.Add(item.Id);
        items.Track(item);
    }
}
