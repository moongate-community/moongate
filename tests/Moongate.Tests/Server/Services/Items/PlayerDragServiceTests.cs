using Moongate.Server.Services.Items;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Services.Items;

public sealed class PlayerDragServiceTests
{
    [Test]
    public void Set_ThenTryGet_ShouldReturnState()
    {
        var service = new PlayerDragService();
        service.SetPending(10, (Serial)0x40000011u, 5, (Serial)0x40000001u, new(12, 34, 0));

        var found = service.TryGet(10, out var state);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(state.ItemId, Is.EqualTo((Serial)0x40000011u));
                Assert.That(state.Amount, Is.EqualTo(5));
                Assert.That(state.SourceContainerId, Is.EqualTo((Serial)0x40000001u));
                Assert.That(state.SourceLocation, Is.EqualTo(new Point3D(12, 34, 0)));
            }
        );
    }

    [Test]
    public void TryConsume_ShouldRemoveState_WhenItemMatches()
    {
        var service = new PlayerDragService();
        service.SetPending(10, (Serial)0x40000022u, 3, (Serial)0x40000002u, new(20, 40, 0));

        var consumed = service.TryConsume(10, (Serial)0x40000022u, out var state);
        var stillExists = service.TryGet(10, out _);

        Assert.Multiple(
            () =>
            {
                Assert.That(consumed, Is.True);
                Assert.That(state.ItemId, Is.EqualTo((Serial)0x40000022u));
                Assert.That(stillExists, Is.False);
            }
        );
    }

    [Test]
    public void TryConsume_ShouldNotRemoveState_WhenItemDoesNotMatch()
    {
        var service = new PlayerDragService();
        service.SetPending(10, (Serial)0x40000033u, 2, (Serial)0x40000003u, new(30, 50, 0));

        var consumed = service.TryConsume(10, (Serial)0x40000044u, out _);
        var stillExists = service.TryGet(10, out var state);

        Assert.Multiple(
            () =>
            {
                Assert.That(consumed, Is.False);
                Assert.That(stillExists, Is.True);
                Assert.That(state.ItemId, Is.EqualTo((Serial)0x40000033u));
            }
        );
    }
}
