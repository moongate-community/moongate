using Moongate.Server.Data.Events.EvenLoop;

namespace Moongate.Tests.Server.Data.Events.EvenLoop;

public class ExecuteBackgroundJobEventTests
{
    [Test]
    public void Constructor_ShouldPopulateFieldsAndTimestamp()
    {
        var gameEvent = new ExecuteBackgroundJobEvent("seed.load.signs");

        Assert.Multiple(
            () =>
            {
                Assert.That(gameEvent.JobName, Is.EqualTo("seed.load.signs"));
                Assert.That(gameEvent.BaseEvent.Timestamp, Is.GreaterThan(0));
            }
        );
    }
}
