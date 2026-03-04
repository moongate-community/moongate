using Moongate.Server.Data.Events.EvenLoop;

namespace Moongate.Tests.Server.Data.Events.EvenLoop;

public class ExecuteMainThreadEventTests
{
    [Test]
    public void Constructor_ShouldPopulateFieldsAndTimestamp()
    {
        var gameEvent = new ExecuteMainThreadEvent("dispatch.mobile.update");

        Assert.Multiple(
            () =>
            {
                Assert.That(gameEvent.ActionName, Is.EqualTo("dispatch.mobile.update"));
                Assert.That(gameEvent.BaseEvent.Timestamp, Is.GreaterThan(0));
            }
        );
    }
}
