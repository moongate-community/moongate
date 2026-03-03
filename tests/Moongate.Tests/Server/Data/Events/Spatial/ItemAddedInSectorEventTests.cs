using Moongate.Server.Data.Events.Spatial;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Data.Events.Spatial;

public class ItemAddedInSectorEventTests
{
    [Test]
    public void Constructor_ShouldPopulateFieldsAndTimestamp()
    {
        var gameEvent = new ItemAddedInSectorEvent((Serial)0x40000010u, 1, 120, 240);

        Assert.Multiple(
            () =>
            {
                Assert.That(gameEvent.ItemId, Is.EqualTo((Serial)0x40000010u));
                Assert.That(gameEvent.MapId, Is.EqualTo(1));
                Assert.That(gameEvent.SectorX, Is.EqualTo(120));
                Assert.That(gameEvent.SectorY, Is.EqualTo(240));
                Assert.That(gameEvent.BaseEvent.Timestamp, Is.GreaterThan(0));
            }
        );
    }
}
