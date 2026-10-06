using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class EquipmentServiceTests
{
    private static readonly Serial Aria = new(0x00000002);

    private readonly ItemService _items = TestItems.Create();
    private readonly EquipmentService _equipment;
    private uint _next = 0x40000100;

    public EquipmentServiceTests()
    {
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                Template("shirt", 0x1517, LayerType.Shirt),
                Template("katana", 0x13FF, LayerType.OneHanded),
                Template("bow", 0x13B2, LayerType.TwoHanded, true),
                Template("heater", 0x1B76, LayerType.TwoHanded),
                Template("backpack", 0x0E75, LayerType.Backpack),
                Template("apple", 0x09D0),
                Template("cap", 0x1715)
            )
        );
        var tiles = new FakeTileDataService()
            .Item(0x09D0, TileFlagType.None, 0)
            .Item(0x1715, TileFlagType.Wearable, 0, layer: (byte)LayerType.Helm);
        _equipment = new(templates, tiles, _items);
    }

    [Fact]
    public void TryGetLayer_UsesTheTemplateLayer_ThenTheWearableTiledataLayer()
    {
        Assert.True(_equipment.TryGetLayer(Item("shirt"), out var shirt));
        Assert.Equal(LayerType.Shirt, shirt);
        Assert.True(_equipment.TryGetLayer(Item("cap"), out var cap));
        Assert.Equal(LayerType.Helm, cap);
        Assert.False(_equipment.TryGetLayer(Item("apple"), out _));
    }

    [Fact]
    public void CanWear_AFreeLayer_IsAllowed()
    {
        Assert.True(_equipment.CanWear(Aria, Item("shirt"), LayerType.Shirt));
    }

    [Fact]
    public void CanWear_TheItemStillOnItsLayer_IsAllowed()
    {
        // A worn item picked up from the paperdoll stays worn until it is dropped, so it may go back on.
        var bow = Item("bow");
        _items.Equip(bow, Aria, LayerType.TwoHanded);

        Assert.True(_equipment.CanWear(Aria, bow, LayerType.TwoHanded));
    }

    [Fact]
    public void CanWear_ATakenLayer_IsRefused()
    {
        Wear("shirt", LayerType.Shirt);

        Assert.False(_equipment.CanWear(Aria, Item("shirt"), LayerType.Shirt));
    }

    [Theory, InlineData(LayerType.Backpack), InlineData(LayerType.Hair), InlineData(LayerType.FacialHair),
     InlineData(LayerType.Mount), InlineData(LayerType.Bank)]
    public void CanWear_ALayerNotWornFromThePaperdoll_IsRefused(LayerType layer)
    {
        Assert.False(_equipment.CanWear(Aria, Item("backpack"), layer));
    }

    [Fact]
    public void CanWear_AShieldWithAOneHandedWeapon_IsAllowed()
    {
        Wear("katana", LayerType.OneHanded);

        Assert.True(_equipment.CanWear(Aria, Item("heater"), LayerType.TwoHanded));
    }

    [Fact]
    public void CanWear_AOneHandedWeaponWithAShield_IsAllowed()
    {
        Wear("heater", LayerType.TwoHanded);

        Assert.True(_equipment.CanWear(Aria, Item("katana"), LayerType.OneHanded));
    }

    [Fact]
    public void CanWear_ATwoHandedWeaponWithAOneHandedItem_IsRefused()
    {
        Wear("katana", LayerType.OneHanded);

        Assert.False(_equipment.CanWear(Aria, Item("bow"), LayerType.TwoHanded));
    }

    [Fact]
    public void CanWear_AOneHandedItemWithATwoHandedWeapon_IsRefused()
    {
        Wear("bow", LayerType.TwoHanded);

        Assert.False(_equipment.CanWear(Aria, Item("katana"), LayerType.OneHanded));
    }

    [Fact]
    public void CanWear_OtherLayersWithATwoHandedWeapon_AreAllowed()
    {
        Wear("bow", LayerType.TwoHanded);

        Assert.True(_equipment.CanWear(Aria, Item("shirt"), LayerType.Shirt));
    }

    private ItemEntity Item(string template)
    {
        var item = new ItemEntity { Id = new(_next++), TemplateId = template, ItemId = 1, Amount = 1 };
        _items.Add([item]);

        return item;
    }

    private void Wear(string template, LayerType layer)
    {
        _items.Equip(Item(template), Aria, layer);
    }

    private static ItemTemplate Template(string id, int itemId, LayerType? layer = null, bool? twoHanded = null)
    {
        return new() { Id = id, ItemId = new Serial((uint)itemId), Layer = layer, TwoHandedWeapon = twoHanded };
    }
}
