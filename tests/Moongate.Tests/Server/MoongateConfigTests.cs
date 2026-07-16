using Moongate.Server.Data.Config;

namespace Moongate.Tests.Server;

public class MoongateConfigTests
{
    [Fact]
    public void Defaults_AreServerReady()
    {
        var config = new MoongateConfig();

        Assert.Equal("0.0.0.0", config.Network.Address);
        Assert.Equal(2593, config.Network.Port);
        Assert.Equal("127.0.0.1", config.Network.PublicAddress);
        Assert.False(string.IsNullOrEmpty(config.ShardName));
    }
}
