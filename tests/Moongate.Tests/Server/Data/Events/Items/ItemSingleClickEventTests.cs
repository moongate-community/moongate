using Moongate.Server.Data.Events.Items;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Data.Events.Items;

public class ItemSingleClickEventTests
{
    [Test]
    public void Constructor_ShouldPopulateFieldsAndTimestamp()
    {
        var gameEvent = new ItemSingleClickEvent(
            17,
            (Serial)0x40000010u
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(gameEvent.SessionId, Is.EqualTo(17));
                Assert.That(gameEvent.ItemSerial, Is.EqualTo((Serial)0x40000010u));
                Assert.That(gameEvent.BaseEvent.Timestamp, Is.GreaterThan(0));
            }
        );
    }
}
