using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Items;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services.Items;

public sealed class InventoryMutationGuardTests
{
    [Fact]
    public void ItemService_ReservedBulkWritesFailBeforeAnyMutation()
    {
        var reservations = new InventoryReservationService(new StubGameLoop());
        Moongate.Server.Ultima.Services.ItemService items = null!;
        var guard = new InventoryMutationGuard(new Lazy<IItemService>(() => items), reservations);
        items = TestItems.Create(inventory: guard);
        var pack = new ItemEntity { Id = new(0x40000001) };
        pack.Equip(new(2), LayerType.Backpack);
        var letter = new ItemEntity { Id = new(0x40000002), Amount = 10 };
        letter.PutInContainer(pack.Id, new Point2D(1, 1));
        items.Add([pack, letter]);
        reservations.TryReserve(new(2), Task.CompletedTask);
        var incoming = new ItemEntity { Id = new(0x40000003) };
        incoming.PutInContainer(pack.Id, new Point2D(1, 1));
        Assert.Throws<InvalidOperationException>(() => items.Add([incoming]));
        Assert.Throws<InvalidOperationException>(() => items.Remove([letter.Id]));
        Assert.Throws<InvalidOperationException>(() => items.MoveToContainer(letter, pack.Id, new(2, 2)));
        Assert.Throws<InvalidOperationException>(() => items.PlaceOnGround(letter, MapType.Trammel, new(0, 0, 0)));
        Assert.Throws<InvalidOperationException>(() => items.Equip(letter, new(2), LayerType.Shirt));
        Assert.Throws<InvalidOperationException>(() => items.Split(letter, 5, new(0x40000003)));
        Assert.Throws<InvalidOperationException>(() => items.Absorb(letter));
        Assert.Equal(10, letter.Amount);
        Assert.Equal(pack.Id, letter.ContainerId);
        Assert.Equal(2, items.Items.Count);
        reservations.Apply(new(2), () => items.Add([incoming]));
        Assert.Equal(3, items.Items.Count);
    }

    [Fact]
    public void Allows_RejectsEitherReservedOwnerAndCorruptAncestry()
    {
        var items = TestItems.Create();
        var reservations = new InventoryReservationService(new StubGameLoop());
        var guard = new InventoryMutationGuard(new Lazy<IItemService>(() => items), reservations);
        var pack = new ItemEntity { Id = new(0x40000001) };
        pack.Equip(new(2), LayerType.Backpack);
        var letter = new ItemEntity { Id = new(0x40000002) };
        letter.PutInContainer(pack.Id, new Point2D(1, 1));
        items.Add([pack, letter]);
        Assert.True(guard.Allows(letter));
        reservations.TryReserve(new(2), Task.CompletedTask);
        Assert.False(guard.Allows(letter));
        Assert.False(guard.Allows(new ItemEntity(), pack.Id));
        Assert.False(guard.AllowsOwner(new(2)));
        reservations.Apply(new(2), () => Assert.True(guard.Allows(letter)));
        reservations.Release(new(2));
        letter.ContainerId = letter.Id;
        Assert.False(guard.Allows(letter));
        letter.PutInContainer(new(0x40009999), new Point2D(1, 1));
        Assert.False(guard.Allows(letter));
    }
}
