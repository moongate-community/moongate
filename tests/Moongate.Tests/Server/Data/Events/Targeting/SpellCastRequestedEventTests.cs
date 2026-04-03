using Moongate.Server.Data.Events.Targeting;

namespace Moongate.Tests.Server.Data.Events.Targeting;

public sealed class SpellCastRequestedEventTests
{
    [Test]
    public void Constructor_ShouldPopulateFieldsAndTimestamp()
    {
        var gameEvent = new SpellCastRequestedEvent(18, 45);

        Assert.Multiple(
            () =>
            {
                Assert.That(gameEvent.SessionId, Is.EqualTo(18));
                Assert.That(gameEvent.SpellId, Is.EqualTo((ushort)45));
                Assert.That(gameEvent.BaseEvent.Timestamp, Is.GreaterThan(0));
            }
        );
    }
}
