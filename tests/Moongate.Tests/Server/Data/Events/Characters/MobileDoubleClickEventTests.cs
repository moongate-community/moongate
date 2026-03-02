using Moongate.Server.Data.Events.Characters;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Data.Events.Characters;

public class MobileDoubleClickEventTests
{
    [Test]
    public void Constructor_ShouldPopulateFieldsAndTimestamp()
    {
        var gameEvent = new MobileDoubleClickEvent(
            18,
            (Serial)0x00000020u
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(gameEvent.SessionId, Is.EqualTo(18));
                Assert.That(gameEvent.MobileSerial, Is.EqualTo((Serial)0x00000020u));
                Assert.That(gameEvent.BaseEvent.Timestamp, Is.GreaterThan(0));
            }
        );
    }
}
