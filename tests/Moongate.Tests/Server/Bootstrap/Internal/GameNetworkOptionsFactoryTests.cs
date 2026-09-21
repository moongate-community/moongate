using System.Net;
using Moongate.Core.Utils;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Services.Network.Framing;

namespace Moongate.Tests.Server.Bootstrap.Internal;

public sealed class GameNetworkOptionsFactoryTests
{
    [Fact]
    public void Create_PreservesEndpointAndCreatesIndependentFramers()
    {
        var options = GameNetworkOptionsFactory.Create(
            new() { Network = new() { ListenAddress = "127.0.0.1", GamePort = 2594 } }
        );
        Assert.Equal(new(IPAddress.Loopback, 2594), Assert.Single(options.Endpoints));
        var first = options.ConnectionPipelineFactory!();
        var second = options.ConnectionPipelineFactory();
        Assert.IsType<UoPacketFramer>(first.Framer);
        Assert.NotSame(first.Framer, second.Framer);
    }

    [Fact]
    public void Create_WildcardPreservesLocalAddressExpansion()
    {
        var options = GameNetworkOptionsFactory.Create(
            new() { Network = new() { ListenAddress = "0.0.0.0", GamePort = 2593 } }
        );
        Assert.Equal(NetworkUtils.GetLocalIpAddresses(), options.Endpoints.Select(endpoint => endpoint.Address));
        Assert.All(options.Endpoints, endpoint => Assert.Equal(2593, endpoint.Port));
    }
}
