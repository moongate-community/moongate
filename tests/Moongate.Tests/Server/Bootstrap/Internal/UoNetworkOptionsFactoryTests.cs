using System.Net;
using Moongate.Core.Utils;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Network.Framing;

namespace Moongate.Tests.Server.Bootstrap.Internal;

public sealed class UoNetworkOptionsFactoryTests
{
    [Fact]
    public void Create_DefaultPortsSeparateLoginAndGame()
    {
        var config = new MoongateServerConfig();

        Assert.Equal(2593, config.Network.LoginPort);
        Assert.Equal(2595, config.Network.GamePort);
    }

    [Fact]
    public void Create_PreservesEndpointAndCreatesIndependentFramers()
    {
        var config = new MoongateServerConfig
        {
            Network = new() { ListenAddress = "127.0.0.1", GamePort = 2594 }
        };
        var options = UoNetworkOptionsFactory.CreateGame(config);
        Assert.Equal(new(IPAddress.Loopback, 2594), Assert.Single(options.Endpoints));
        var first = options.ConnectionPipelineFactory!();
        var second = options.ConnectionPipelineFactory();
        Assert.IsType<GameSeedFramer>(first.Framer);
        Assert.NotSame(first.Framer, second.Framer);
    }

    [Fact]
    public void Create_WildcardPreservesLocalAddressExpansion()
    {
        var config = new MoongateServerConfig
        {
            Network = new() { ListenAddress = "0.0.0.0", GamePort = 2593 }
        };
        var options = UoNetworkOptionsFactory.CreateGame(config);
        Assert.Equal(NetworkUtils.GetLocalIpAddresses(), options.Endpoints.Select(endpoint => endpoint.Address));
        Assert.All(options.Endpoints, endpoint => Assert.Equal(2593, endpoint.Port));
    }

    [Fact]
    public void Create_LoginAndGameUseDistinctPortsForEveryLocalAddress()
    {
        var config = new MoongateServerConfig
        {
            Network = new() { ListenAddress = "0.0.0.0", LoginPort = 4000, GamePort = 5000 }
        };

        var login = UoNetworkOptionsFactory.CreateLogin(config);
        var game = UoNetworkOptionsFactory.CreateGame(config);
        var addresses = NetworkUtils.GetLocalIpAddresses();

        Assert.Equal(addresses, login.Endpoints.Select(endpoint => endpoint.Address));
        Assert.Equal(addresses, game.Endpoints.Select(endpoint => endpoint.Address));
        Assert.All(login.Endpoints, endpoint => Assert.Equal(4000, endpoint.Port));
        Assert.All(game.Endpoints, endpoint => Assert.Equal(5000, endpoint.Port));
        Assert.IsType<UoPacketFramer>(login.ConnectionPipelineFactory!().Framer);
        Assert.IsType<GameSeedFramer>(game.ConnectionPipelineFactory!().Framer);
    }
}
