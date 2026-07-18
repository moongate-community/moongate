using Moongate.Server.Services.Accounts;

namespace Moongate.Tests.Server.Accounts;

public class SessionManagerTests
{
    [Fact]
    public void All_OnFreshManager_IsEmpty()
    {
        var manager = new SessionManager();

        Assert.Empty(manager.All);
        Assert.Equal(0, manager.Count);
    }
}
