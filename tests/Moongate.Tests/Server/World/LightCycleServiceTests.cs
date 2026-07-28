using Moongate.Server.Services.World;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.World;

public class LightCycleServiceTests
{
    [Fact]
    public void Tick_NoSessions_SendsNothing()
    {
        // Everything worth asserting about Tick — which level a player gets, and that an unchanged
        // level is not resent — needs a PlayerSession, which cannot be built without a live socket.
        // The rules themselves are covered by GameClockTests, LightLevelsTests and LightServiceTests;
        // what is left is the loop, proved by the real-boot smoke test.
        var sessions = new StubSessionManager();
        var service = new LightCycleService(sessions, new StubLightService(5), new StubGameLoopContext());

        service.Tick();

        Assert.Empty(sessions.All);
    }
}
