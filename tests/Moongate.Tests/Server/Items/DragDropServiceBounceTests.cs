using Moongate.Core.Primitives;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Items;

public class DragDropServiceBounceTests
{
    [Fact]
    public void Bounce_ToItsOrigin_PutsTheItemBackInTheSameContainerSlot()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(amount: 1);
        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 1, Serial.Zero, out var heldId, out var origin);

        fixture.Service.Bounce(fixture.Actor, heldId, origin);

        Assert.Equal(fixture.Backpack.Id, fixture.Gold.ParentContainerId);
        Assert.Equal(origin!.ContainerPosition, fixture.Gold.ContainerPosition);
    }

    [Fact]
    public void Bounce_WhenTheOriginContainerIsGone_FallsBackToTheBackpack()
    {
        var fixture = DragDropFixture.WithBagInBackpack();
        var coin = fixture.PutGoldInTheBag(amount: 1);
        fixture.Service.Lift(fixture.Actor, coin.Id, 1, Serial.Zero, out var heldId, out var origin);

        fixture.DeleteTheBag();

        fixture.Service.Bounce(fixture.Actor, heldId, origin);

        Assert.Equal(fixture.Backpack.Id, coin.ParentContainerId);
    }

    [Fact]
    public void Bounce_WithNoOriginAndNoBackpack_DropsItAtTheActorsFeet()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(amount: 1);
        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 1, Serial.Zero, out var heldId, out _);

        fixture.RemoveTheBackpack();

        fixture.Service.Bounce(fixture.Actor, heldId, origin: null);

        Assert.Equal(fixture.Actor.MapId, fixture.Gold.MapId);
        Assert.Equal(fixture.Actor.Position, fixture.Gold.Position);
    }
}
