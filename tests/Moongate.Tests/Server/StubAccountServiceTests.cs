using Moongate.Network.Types;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server;

public class StubAccountServiceTests
{
    [Fact]
    public void Authenticate_EmptyPassword_IsDenied()
    {
        var result = new StubAccountService().Authenticate("squid", "");

        Assert.False(result.Success);
        Assert.Equal(LoginDeniedReasonType.IncorrectCredentials, result.Reason);
    }

    [Fact]
    public void Authenticate_NonEmptyCredentials_Succeeds()
    {
        var result = new StubAccountService().Authenticate("squid", "secret");

        Assert.True(result.Success);
        Assert.Equal("squid", result.Username);
    }
}
