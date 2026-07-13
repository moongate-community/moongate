using Moongate.Server.Services.Accounts;

namespace Moongate.Tests.Server;

public class SessionManagerTests
{
    [Fact]
    public void TryGet_UnknownSession_ReturnsFalse()
    {
        var manager = new SessionManager();

        Assert.False(manager.TryGet(123L, out _));
        Assert.Equal(0, manager.Count);
    }
}
