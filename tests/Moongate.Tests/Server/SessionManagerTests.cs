using Moongate.Core.Primitives;
using Moongate.Server.Services.Accounts;

namespace Moongate.Tests.Server;

public class SessionManagerTests
{
    [Fact]
    public void IsCharacterPlayed_WithNobodyConnected_ReturnsFalse()
        => Assert.False(new SessionManager().IsCharacterPlayed((Serial)0x64));

    [Fact]
    public void TryGet_UnknownSession_ReturnsFalse()
    {
        var manager = new SessionManager();

        Assert.False(manager.TryGet(123L, out _));
        Assert.Equal(0, manager.Count);
    }
}
