using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Items;

/// <summary>
/// Splitting a pile and putting the piece down somewhere else. The gesture only exists because the two
/// halves stay apart: a player counting coins out drops them on an empty spot, and a drop that hunts
/// the container for anything it could merge with undoes the split before the client can draw it.
/// </summary>
public class SplitStackPlacementTests
{
    private static readonly Point2D EmptySpot = new(46, 102);

    private static readonly Point2D WhereTheStackIs = new(127, 100);

    /// <summary>The client sends 0xFFFF for both coordinates when the item was released on the bag itself.</summary>
    private static readonly Point2D OnTheContainerItself = new(0xFFFF, 0xFFFF);

    [Fact]
    public void DroppingASplitPieceOnAnEmptySpot_LeavesTwoStacks()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(1000);

        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 100, Serial.Zero, out var heldId, out _);
        fixture.Service.Drop(fixture.Actor, heldId, fixture.Backpack.Id, Point3D.Zero, EmptySpot);

        var stacks = fixture.BackpackContents().Select(item => item.Amount).OrderBy(amount => amount).ToList();

        Assert.Equal([100, 900], stacks);
    }

    [Fact]
    public void DroppingASplitPieceOnAnEmptySpot_LeavesItWhereItWasPut()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(1000);

        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 100, Serial.Zero, out var heldId, out _);
        fixture.Service.Drop(fixture.Actor, heldId, fixture.Backpack.Id, Point3D.Zero, EmptySpot);

        var piece = fixture.BackpackContents().Single(item => item.Amount == 100);

        Assert.Equal(EmptySpot, piece.ContainerPosition);
    }

    /// <summary>Released on the bag rather than inside it, there is no spot to honour, so piles join.</summary>
    [Fact]
    public void DroppingOnTheContainerItself_MergesWithWhatIsAlreadyThere()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(1000);

        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 100, Serial.Zero, out var heldId, out _);
        fixture.Service.Drop(fixture.Actor, heldId, fixture.Backpack.Id, Point3D.Zero, OnTheContainerItself);

        var stack = Assert.Single(fixture.BackpackContents());

        Assert.Equal(1000, stack.Amount);
    }

    /// <summary>
    /// The other way to put the halves back together: drop the piece onto the pile, which the client
    /// reports by naming the pile.
    /// </summary>
    [Fact]
    public void DroppingASplitPieceBackOntoTheStack_MergesThemAgain()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(1000);

        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 100, Serial.Zero, out var heldId, out _);

        var remainder = fixture.BackpackContents().Single(item => item.Id != heldId);

        fixture.Service.Drop(fixture.Actor, heldId, remainder.Id, Point3D.Zero, WhereTheStackIs);

        var stack = Assert.Single(fixture.BackpackContents());

        Assert.Equal(1000, stack.Amount);
    }
}
