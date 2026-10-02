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

    [Theory, InlineData(-1), InlineData(0), InlineData(65536)]
    public void Validate_PingPortOutOfRange_Throws(int port)
    {
        var config = new NetworkConfig { PingPort = port };

        Assert.Throws<InvalidOperationException>(() => config.Validate(ServerMode.Standalone));
    }

    [Fact]
    public void Validate_PingPortOutOfRange_IsAcceptedWhenThePingServerIsOff()
    {
        var config = new NetworkConfig { EnablePingServer = false, PingPort = -1 };

        config.Validate(ServerMode.Standalone);
    }

    [Fact]
    public void ToPingServerOptions_UsesTheListenAddressAndThePingPort()
    {
        var config = new NetworkConfig { ListenAddress = "127.0.0.1", PingPort = 12001 };

        var options = config.ToPingServerOptions();

        Assert.True(options.Enabled);
        Assert.Equal(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 12001), Assert.Single(options.Endpoints));
    }

    [Fact]
    public void PingPort_DefaultsTo12000()
    {
        Assert.Equal(12000, new NetworkConfig().PingPort);
    }

    [Theory, InlineData(ServerMode.Login), InlineData(ServerMode.Game)]
    public void Validate_SingleRoleAllowsSameConfiguredPort(ServerMode mode)
    {
        var config = new NetworkConfig { LoginPort = 2593, GamePort = 2593 };

        config.Validate(mode);
    }
}
