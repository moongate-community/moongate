using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Items;

/// <summary>
/// Two stacks that were never one, merged by hand — a player dragging one pile of gold onto another.
/// The existing merge test splits a stack and rejoins it, which exercises a different path: there the
/// dropped entity was born a moment earlier from the very stack it returns to.
/// </summary>
public class DragDropStackMergeTests
{
    [Fact]
    public void DroppingAStackOntoAnother_KeepsEveryCoin()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(500);
        var second = SecondGoldStack(fixture, 500);

        fixture.Service.Lift(fixture.Actor, second.Id, 500, Serial.Zero, out var heldId, out _);
        fixture.Service.Drop(fixture.Actor, heldId, fixture.Backpack.Id, Point3D.Zero, new(50, 70));

        var total = fixture.BackpackContents().Sum(item => item.Amount);

        Assert.Equal(1000, total);
    }

    [Fact]
    public void DroppingAStackOntoAnother_LeavesOneStack()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(500);
        var second = SecondGoldStack(fixture, 500);

        fixture.Service.Lift(fixture.Actor, second.Id, 500, Serial.Zero, out var heldId, out _);
        fixture.Service.Drop(fixture.Actor, heldId, fixture.Backpack.Id, Point3D.Zero, new(50, 70));

        var stack = Assert.Single(fixture.BackpackContents());

        Assert.Equal(1000, stack.Amount);
    }

    // The container keeps a list of what it holds. A merge that deletes the absorbed entity without
    // unlisting it leaves the backpack pointing at something that no longer exists.
    [Fact]
    public void DroppingAStackOntoAnother_LeavesNoDanglingIdInTheContainer()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(500);
        var second = SecondGoldStack(fixture, 500);

        fixture.Service.Lift(fixture.Actor, second.Id, 500, Serial.Zero, out var heldId, out _);
        fixture.Service.Drop(fixture.Actor, heldId, fixture.Backpack.Id, Point3D.Zero, new(50, 70));

        var listed = fixture.Items.GetById(fixture.Backpack.Id)!.ContainedItemIds;

        Assert.All(listed, id => Assert.NotNull(fixture.Items.GetById(id)));
    }

    private static ItemEntity SecondGoldStack(DragDropFixture fixture, int amount)
    {
        var gold = new ItemEntity { TemplateId = "gold", ItemId = 3821, Amount = amount, Hue = new(0) };

        fixture.Items.Create(gold);
        fixture.Items.AddToContainer(fixture.Backpack, gold, new(60, 70));

        return gold;
    }
}
