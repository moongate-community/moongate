using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Config.Sections;

namespace Moongate.Tests.Server.Data.Config.Sections;

public sealed class NetworkConfigTests
{
    [Fact]
    public void Validate_StandaloneRejectsSharedLoginAndGamePort()
    {
        var config = new MoongateServerConfig
        {
            Mode = ServerMode.Standalone,
            Network = new() { LoginPort = 2593, GamePort = 2593 }
        };

        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Theory, InlineData(ServerMode.Login), InlineData(ServerMode.Game)]
    public void Validate_SingleRoleAllowsSameConfiguredPort(ServerMode mode)
    {
        var config = new NetworkConfig { LoginPort = 2593, GamePort = 2593 };

        config.Validate(mode);
    }
}
