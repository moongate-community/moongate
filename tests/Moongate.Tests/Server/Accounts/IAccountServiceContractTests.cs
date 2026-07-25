using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Types;
using Moongate.Tests.Support;
using Xunit;

namespace Moongate.Tests.Server.Accounts;

public sealed class IAccountServiceContractTests
{
    [Fact]
    public void ResendVerification_ImplementerWithoutOverride_ReturnsIgnored()
    {
        IAccountService service = new StubAccountService();

        Assert.Equal(AccountResendResultType.Ignored, service.ResendVerification("newbie", "new@bie.test"));
    }
}
