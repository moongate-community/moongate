using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Utils;

namespace Moongate.Tests.Server.Ultima.Utils;

public sealed class ContainerSlotUtilsTests
{
    private static readonly Serial Backpack = new(0x40000001);

    [Fact]
    public void FirstFree_AnEmptyContainer_GivesTheWantedSlot()
    {
        Assert.Equal(0, ContainerSlotUtils.FirstFree([]));
        Assert.Equal(7, ContainerSlotUtils.FirstFree([], 7));
    }

    [Fact]
    public void FirstFree_TheWantedSlotIsTaken_GivesTheNextFreeOne()
    {
        var contents = new[] { InSlot(0), InSlot(1), InSlot(3) };

        Assert.Equal(2, ContainerSlotUtils.FirstFree(contents));
        Assert.Equal(4, ContainerSlotUtils.FirstFree(contents, 3));
    }

    [Fact]
    public void FirstFree_TheLastSlotIsTaken_WrapsToTheFirstFreeOne()
    {
        var contents = new[] { InSlot(124), InSlot(0) };

        Assert.Equal(1, ContainerSlotUtils.FirstFree(contents, 124));
    }

    [Fact]
    public void FirstFree_ASlotBeyondTheGrid_StartsFromTheFirst()
    {
        Assert.Equal(0, ContainerSlotUtils.FirstFree([], 200));
    }

    [Fact]
    public void FirstFree_AFullContainer_SharesTheWantedSlot()
    {
        // More items than the grid of the Enhanced Client has slots: they overlap there, the classic client is not affected.
        var contents = Enumerable.Range(0, ContainerSlotUtils.SlotCount).Select(slot => InSlot((byte)slot)).ToArray();

        Assert.Equal(5, ContainerSlotUtils.FirstFree(contents, 5));
    }

    private static ItemEntity InSlot(byte slot)
    {
        var item = new ItemEntity { Id = new(0x40000100u + slot) };
        item.PutInContainer(Backpack, new Point2D(10, 10), slot);

        return item;
    }
}
