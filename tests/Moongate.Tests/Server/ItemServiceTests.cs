using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.Game;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server;

public class ItemServiceTests
{
    [Fact]
    public void AddToContainer_RemovesItemFromSpatialIndex()
    {
        var (service, spatial, _) = BuildWithSpatial();
        var container = Item("Backpack", 3701);
        var item = Item();
        item.MapId = 0;
        item.Position = new(100, 100, 0);
        service.Create(container);
        service.Create(item);

        service.AddToContainer(container, item, new(1, 1));

        Assert.Empty(spatial.GetItemsInRange(0, new(100, 100, 0), 5));
    }

    [Fact]
    public void AddToContainer_SetsBothSidesAndClearsEquip()
    {
        var (service, _) = Build();
        var container = Item("Backpack", 3701);
        var item = Item();
        service.Create(container);

        service.AddToContainer(container, item, new(3, 4));

        Assert.Equal(container.Id, item.ParentContainerId);
        Assert.Equal(new(3, 4), item.ContainerPosition);
        Assert.Equal(Serial.Zero, item.EquippedMobileId);
        Assert.Contains(item.Id, container.ContainedItemIds);
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
    public void Create_GroundItem_IsSpatiallyQueryable()
    {
        var (service, spatial, _) = BuildWithSpatial();
        var item = Item();
        item.MapId = 0;
        item.Position = new(100, 100, 0);

        service.Create(item);

        Assert.Single(spatial.GetItemsInRange(0, new(100, 100, 0), 5));
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

    [Fact]
    public void Delete_RemovesItemFromSpatialIndex()
    {
        var (service, spatial, _) = BuildWithSpatial();
        var item = Item();
        item.MapId = 0;
        item.Position = new(100, 100, 0);
        service.Create(item);

        service.Delete(item.Id);

        Assert.Empty(spatial.GetItemsInRange(0, new(100, 100, 0), 5));
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
        service.AddToContainer(container, item, new(1, 2));

        service.Equip(mobile, item, LayerType.OneHanded);

        Assert.Equal(Serial.Zero, item.ParentContainerId);
        Assert.Equal(LayerType.OneHanded, item.EquippedLayer);
        Assert.DoesNotContain(item.Id, container.ContainedItemIds);
    }

    [Fact]
    public void Equip_RemovesItemFromSpatialIndex()
    {
        var (service, spatial, persistence) = BuildWithSpatial();
        var mobile = new MobileEntity { Id = new(0x1), MapId = 0, Position = new(100, 100, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
        var item = Item();
        item.MapId = 0;
        item.Position = new(100, 100, 0);
        service.Create(item);

        service.Equip(mobile, item, LayerType.OneHanded);

        Assert.Empty(spatial.GetItemsInRange(0, new(100, 100, 0), 5));
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
    public void Flip_CyclesToNextVariantAndPersists()
    {
        var (service, persistence) = Build();
        var item = new ItemEntity { Name = "Armoire", ItemId = 2639, FlippableItemIds = [2639, 2643] };
        service.Create(item);

        Assert.True(service.Flip(item));
        Assert.Equal(2643, item.ItemId);
        Assert.Equal(2643, persistence.Store<ItemEntity>().GetById(item.Id)!.ItemId);
    }

    [Fact]
    public void Flip_ReturnsFalse_WhenCurrentIdNotInList()
    {
        var (service, _) = Build();
        var item = new ItemEntity { Name = "Odd", ItemId = 100, FlippableItemIds = [2639, 2643] };

        Assert.False(service.Flip(item));
        Assert.Equal(100, item.ItemId);
    }

    [Fact]
    public void Flip_ReturnsFalse_WhenFewerThanTwoVariants()
    {
        var (service, _) = Build();
        var item = new ItemEntity { Name = "Dagger", ItemId = 3921, FlippableItemIds = [] };

        Assert.False(service.Flip(item));
        Assert.Equal(3921, item.ItemId);
    }

    [Fact]
    public void Flip_WrapsAroundToFirstVariant()
    {
        var (service, _) = Build();
        var item = new ItemEntity { Name = "Armoire", ItemId = 2643, FlippableItemIds = [2639, 2643] };
        service.Create(item);

        Assert.True(service.Flip(item));
        Assert.Equal(2639, item.ItemId);
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
    public void RemoveFromContainer_ClearsBothSides()
    {
        var (service, _) = Build();
        var container = Item("Backpack", 3701);
        var item = Item();
        service.Create(container);
        service.AddToContainer(container, item, new(1, 1));

        service.RemoveFromContainer(container, item);

        Assert.DoesNotContain(item.Id, container.ContainedItemIds);
        Assert.Equal(Serial.Zero, item.ParentContainerId);
    }

    [Fact]
    public void RemoveFromContainer_MakesItemSpatiallyQueryableAgain()
    {
        var (service, spatial, _) = BuildWithSpatial();
        var container = Item("Backpack", 3701);
        var item = Item();
        item.MapId = 0;
        item.Position = new(100, 100, 0);
        service.Create(container);
        service.AddToContainer(container, item, new(1, 1));

        service.RemoveFromContainer(container, item);

        Assert.Single(spatial.GetItemsInRange(0, new(100, 100, 0), 5));
    }

    [Fact]
    public void Save_UpdatesTheItemPositionInTheSpatialIndex()
    {
        var (service, spatial, _) = BuildWithSpatial();
        var item = Item();
        item.MapId = 0;
        item.Position = new(100, 100, 0);
        service.Create(item);

        item.Position = new(300, 300, 0);
        service.Save(item);

        Assert.Empty(spatial.GetItemsInRange(0, new(100, 100, 0), 5));
        Assert.Single(spatial.GetItemsInRange(0, new(300, 300, 0), 5));
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

        Assert.Null(service.Unequip(new() { Name = "Bob" }, LayerType.OneHanded));
    }

    [Fact]
    public void Unequip_MakesItemSpatiallyQueryableAgain()
    {
        var (service, spatial, persistence) = BuildWithSpatial();
        var mobile = new MobileEntity { Id = new(0x1), MapId = 0, Position = new(100, 100, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
        var item = Item();
        item.MapId = 0;
        item.Position = new(100, 100, 0);
        service.Equip(mobile, item, LayerType.OneHanded);

        service.Unequip(mobile, LayerType.OneHanded);

        Assert.Single(spatial.GetItemsInRange(0, new(100, 100, 0), 5));
    }

    private static (ItemService Service, FakePersistenceService Persistence) Build()
    {
        var persistence = new FakePersistenceService();

        return (new(persistence), persistence);
    }

    private static (ItemService Service, SpatialIndexService Spatial, FakePersistenceService Persistence) BuildWithSpatial()
    {
        var persistence = new FakePersistenceService();
        var marker = new LoopThreadMarker();
        marker.Capture();
        var spatial = new SpatialIndexService(persistence, marker);

        return (new(persistence, null, spatial), spatial, persistence);
    }

    private static ItemEntity Item(string name = "Dagger", int itemId = 3921)
        => new() { Name = name, ItemId = itemId };
}
