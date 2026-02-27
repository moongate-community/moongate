using Moongate.Server.Data.Events.Items;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Data.Events.Items;

public class ItemDoubleClickEventTests
{
    [Test]
    public void Constructor_ShouldPopulateFieldsAndTimestamp()
    {
        var gameEvent = new ItemDoubleClickEvent(
            18,
            (Serial)0x40000020u
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(gameEvent.SessionId, Is.EqualTo(18));
                Assert.That(gameEvent.ItemSerial, Is.EqualTo((Serial)0x40000020u));
                Assert.That(gameEvent.BaseEvent.Timestamp, Is.GreaterThan(0));
            }
        );
    }
}
