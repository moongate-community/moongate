using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;

namespace Moongate.Tests.Server.Data.Config;

public sealed class MoongateServerConfigTests
{
    [Theory, InlineData(0), InlineData(4), InlineData(5), InlineData(-1)]
    public void Validate_EmptyOrUnknownMode_RejectsBeforeServerStartup(int mode)
    {
        var config = new MoongateServerConfig { Mode = (ServerMode)mode };

        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Fact]
    public void Validate_LoginModeRequiresEnabledInternalApi()
    {
        var config = new MoongateServerConfig { Mode = ServerMode.Login };

        Assert.Contains("api.enabled", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }
}
