using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Items;

/// <summary>
/// Dropping onto an item rather than into a container. The 0x08 packet carries one serial for both
/// gestures: release over a bag and the client sends the bag, release over a pile of gold and it sends
/// the pile. The other merge tests all pass the backpack, which is the gesture the player did not make.
/// </summary>
public class DropOnItemTests
{
    [Fact]
    public void DroppingGoldOntoGold_MergesIntoTheTargetStack()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(500);
        var second = fixture.AddGoldToBackpack(500, new(60, 70));

        fixture.Service.Lift(fixture.Actor, second.Id, 500, Serial.Zero, out var heldId, out _);
        fixture.Service.Drop(fixture.Actor, heldId, fixture.Gold.Id, Point3D.Zero, new(50, 70));

        var stack = Assert.Single(fixture.BackpackContents());

        Assert.Equal(1000, stack.Amount);
    }

    // Gold is not a bag. Nesting the coins inside the target hides them from a client that has no way
    // to open it, and leaves the target drawn at its old amount -- the pile reads as eaten.
    [Fact]
    public void DroppingGoldOntoGold_PutsNothingInsideTheTargetStack()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(500);
        var second = fixture.AddGoldToBackpack(500, new(60, 70));

        fixture.Service.Lift(fixture.Actor, second.Id, 500, Serial.Zero, out var heldId, out _);
        fixture.Service.Drop(fixture.Actor, heldId, fixture.Gold.Id, Point3D.Zero, new(50, 70));

        Assert.Empty(fixture.Items.GetContents(fixture.Gold.Id));
    }

    /// <summary>
    /// The same gesture with something that cannot merge: it belongs beside the target, in whatever
    /// holds the target, not inside it.
    /// </summary>
    [Fact]
    public void DroppingAnItemOntoAPlainItem_LandsBesideItInTheSameContainer()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(500);
        var sword = fixture.AddSwordToBackpack(new(60, 70));

        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 500, Serial.Zero, out var heldId, out _);
        fixture.Service.Drop(fixture.Actor, heldId, sword.Id, Point3D.Zero, new(50, 70));

        Assert.Empty(fixture.Items.GetContents(sword.Id));
        Assert.Contains(fixture.BackpackContents(), item => item.Id == heldId);
    }

    /// <summary>A bag is a container, so the gesture keeps meaning what it always meant.</summary>
    [Fact]
    public void DroppingAnItemOntoABag_StillGoesInside()
    {
        var fixture = DragDropFixture.WithBagInBackpack();
        var gold = fixture.AddGoldToBackpack(500, new(60, 70));

        fixture.Service.Lift(fixture.Actor, gold.Id, 500, Serial.Zero, out var heldId, out _);
        fixture.Service.Drop(fixture.Actor, heldId, fixture.Bag.Id, Point3D.Zero, new(10, 10));

        Assert.Contains(fixture.Items.GetContents(fixture.Bag.Id), item => item.Id == heldId);
    }
}
