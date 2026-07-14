using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server;

public class ItemServiceTests
{
    private static (ItemService Service, FakePersistenceService Persistence) Build()
    {
        var persistence = new FakePersistenceService();

        return (new ItemService(persistence), persistence);
    }

    private static ItemEntity Item(string name = "Dagger", int itemId = 3921)
    {
        return new ItemEntity { Name = name, ItemId = itemId };
    }

    [Fact]
    public void Create_AllocatesSerialAndPersists()
    {
        var (service, persistence) = Build();
        var item = Item();

        var id = service.Create(item);

        Assert.NotEqual(Serial.Zero, id);
        Assert.Equal(id, item.Id);
        Assert.NotNull(persistence.Store<ItemEntity>().GetById(id));
    }

    [Fact]
    public void Equip_SetsBothSidesAndPersists()
    {
        var (service, persistence) = Build();
        var mobile = new MobileEntity { Name = "Bob" };
        var item = Item();

        service.Equip(mobile, item, LayerType.OneHanded);

        Assert.Equal(item.Id, mobile.EquippedItemIds[LayerType.OneHanded]);
        Assert.Equal(mobile.Id, item.EquippedMobileId);
        Assert.Equal(LayerType.OneHanded, item.EquippedLayer);
        Assert.Equal(Serial.Zero, item.ParentContainerId);
        Assert.NotNull(persistence.Store<ItemEntity>().GetById(item.Id));
    }

    [Fact]
    public void Equip_Backpack_SetsBackpackId()
    {
        var (service, _) = Build();
        var mobile = new MobileEntity { Name = "Bob" };
        var pack = Item("Backpack", 3701);

        service.Equip(mobile, pack, LayerType.Backpack);

        Assert.Equal(pack.Id, mobile.BackpackId);
        Assert.Equal(pack.Id, mobile.EquippedItemIds[LayerType.Backpack]);
    }

    [Fact]
    public void Equip_FromContainer_DetachesFromOldContainer()
    {
        var (service, _) = Build();
        var mobile = new MobileEntity { Name = "Bob" };
        var container = Item("Backpack", 3701);
        var item = Item();
        service.Create(container);
        service.AddToContainer(container, item, new Point2D(1, 2));

        service.Equip(mobile, item, LayerType.OneHanded);

        Assert.Equal(Serial.Zero, item.ParentContainerId);
        Assert.Equal(LayerType.OneHanded, item.EquippedLayer);
        Assert.DoesNotContain(item.Id, container.ContainedItemIds);
    }

    [Fact]
    public void Unequip_ClearsBothSidesAndReturnsItem()
    {
        var (service, _) = Build();
        var mobile = new MobileEntity { Name = "Bob" };
        var pack = Item("Backpack", 3701);
        service.Equip(mobile, pack, LayerType.Backpack);

        var detached = service.Unequip(mobile, LayerType.Backpack);

        Assert.Same(pack, detached);
        Assert.False(mobile.EquippedItemIds.ContainsKey(LayerType.Backpack));
        Assert.Equal(Serial.Zero, mobile.BackpackId);
        Assert.Equal(Serial.Zero, pack.EquippedMobileId);
        Assert.Null(pack.EquippedLayer);
    }

    [Fact]
    public void Unequip_EmptyLayer_ReturnsNull()
    {
        var (service, _) = Build();

        Assert.Null(service.Unequip(new MobileEntity { Name = "Bob" }, LayerType.OneHanded));
    }

    [Fact]
    public void AddToContainer_SetsBothSidesAndClearsEquip()
    {
        var (service, _) = Build();
        var container = Item("Backpack", 3701);
        var item = Item();
        service.Create(container);

        service.AddToContainer(container, item, new Point2D(3, 4));

        Assert.Equal(container.Id, item.ParentContainerId);
        Assert.Equal(new Point2D(3, 4), item.ContainerPosition);
        Assert.Equal(Serial.Zero, item.EquippedMobileId);
        Assert.Contains(item.Id, container.ContainedItemIds);
    }

    [Fact]
    public void RemoveFromContainer_ClearsBothSides()
    {
        var (service, _) = Build();
        var container = Item("Backpack", 3701);
        var item = Item();
        service.Create(container);
        service.AddToContainer(container, item, new Point2D(1, 1));

        service.RemoveFromContainer(container, item);

        Assert.DoesNotContain(item.Id, container.ContainedItemIds);
        Assert.Equal(Serial.Zero, item.ParentContainerId);
    }

    [Fact]
    public void GetContents_ResolvesContainedItems()
    {
        var (service, _) = Build();
        var container = Item("Backpack", 3701);
        var a = Item("A", 1);
        var b = Item("B", 2);
        service.Create(container);
        service.AddToContainer(container, a, Point2D.Zero);
        service.AddToContainer(container, b, Point2D.Zero);

        var contents = service.GetContents(container.Id);

        Assert.Equal(2, contents.Count);
        Assert.Contains(contents, i => i.Id == a.Id);
        Assert.Contains(contents, i => i.Id == b.Id);
    }

    [Fact]
    public void GetEquipped_ResolvesEquippedItems()
    {
        var (service, _) = Build();
        var mobile = new MobileEntity { Name = "Bob" };
        service.Equip(mobile, Item("Sword", 1), LayerType.OneHanded);
        service.Equip(mobile, Item("Shield", 2), LayerType.TwoHanded);

        Assert.Equal(2, service.GetEquipped(mobile).Count);
    }

    [Fact]
    public void Delete_RemovesFromStore()
    {
        var (service, persistence) = Build();
        var item = Item();
        var id = service.Create(item);

        Assert.True(service.Delete(id));
        Assert.Null(persistence.Store<ItemEntity>().GetById(id));
    }
}
