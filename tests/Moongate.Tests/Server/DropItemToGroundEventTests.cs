using Moongate.Server.Data.Events;
using Moongate.Server.Data.Events.Items;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server;

public class DropItemToGroundEventTests
{
    [Test]
    public void Constructor_ShouldPopulateFieldsAndTimestamp()
    {
        var oldLocation = new Point3D(100, 200, 5);
        var newLocation = new Point3D(101, 201, 0);

        var gameEvent = new DropItemToGroundEvent(
            sessionId: 7,
            mobileId: (Serial)0x00000042u,
            itemId: (Serial)0x40000010u,
            sourceContainerId: (Serial)0x40000022u,
            oldLocation: oldLocation,
            newLocation: newLocation
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(gameEvent.SessionId, Is.EqualTo(7));
                Assert.That(gameEvent.MobileId, Is.EqualTo((Serial)0x00000042u));
                Assert.That(gameEvent.ItemId, Is.EqualTo((Serial)0x40000010u));
                Assert.That(gameEvent.SourceContainerId, Is.EqualTo((Serial)0x40000022u));
                Assert.That(gameEvent.OldLocation, Is.EqualTo(oldLocation));
                Assert.That(gameEvent.NewLocation, Is.EqualTo(newLocation));
                Assert.That(gameEvent.BaseEvent.Timestamp, Is.GreaterThan(0));
            }
        );
    }
}
