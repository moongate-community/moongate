using System.Net;
using Moongate.Core.Utils;
using Moongate.Network.Client;
using Moongate.Network.Interfaces.Middleware;
using Moongate.Network.Packets.Registry;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Network.Framing;
using Moongate.Server.Services.Network.Middleware;
using Moongate.Server.Types.Network;

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
        var options = UoNetworkOptionsFactory.CreateGame(config, PacketRegistry.Default, []);
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
        var options = UoNetworkOptionsFactory.CreateGame(config, PacketRegistry.Default, []);
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

        var login = UoNetworkOptionsFactory.CreateLogin(config, PacketRegistry.Default);
        var game = UoNetworkOptionsFactory.CreateGame(config, PacketRegistry.Default, []);
        var addresses = NetworkUtils.GetLocalIpAddresses();

        Assert.Equal(addresses, login.Endpoints.Select(endpoint => endpoint.Address));
        Assert.Equal(addresses, game.Endpoints.Select(endpoint => endpoint.Address));
        Assert.All(login.Endpoints, endpoint => Assert.Equal(4000, endpoint.Port));
        Assert.All(game.Endpoints, endpoint => Assert.Equal(5000, endpoint.Port));
        Assert.IsType<UoPacketFramer>(login.ConnectionPipelineFactory!().Framer);
        Assert.IsType<GameSeedFramer>(game.ConnectionPipelineFactory!().Framer);
    }

    [Fact]
    public void Create_GameAttachesTheGivenMiddlewaresAndLoginAttachesNone()
    {
        var config = new MoongateServerConfig
        {
            Network = new() { ListenAddress = "127.0.0.1" }
        };
        var middleware = new PassThroughMiddleware();

        var game = UoNetworkOptionsFactory.CreateGame(config, PacketRegistry.Default, [middleware]);
        var login = UoNetworkOptionsFactory.CreateLogin(config, PacketRegistry.Default);

        Assert.Same(middleware, Assert.Single(game.ConnectionPipelineFactory!().Middlewares!));
        Assert.Null(login.ConnectionPipelineFactory!().Middlewares);
    }

    [Fact]
    public void Create_EnabledEncryptionIsFreshPerConnectionAndFollowsExistingMiddleware()
    {
        var config = new MoongateServerConfig { Network = new() { ListenAddress = "127.0.0.1" } };
        config.Network.Encryption.Mode = NetworkEncryptionMode.Optional;
        config.Network.Encryption.ClientVersion = "67.0.117.0";
        var preceding = new PassThroughMiddleware();
        var supplied = new List<INetMiddleware> { preceding };
        var game = UoNetworkOptionsFactory.CreateGame(config, PacketRegistry.Default, supplied);
        var login = UoNetworkOptionsFactory.CreateLogin(config, PacketRegistry.Default);
        config.Network.Encryption.Mode = NetworkEncryptionMode.Disabled;
        config.Network.Encryption.ClientVersion = "bad";
        supplied.Clear();
        var first = game.ConnectionPipelineFactory!();
        var second = game.ConnectionPipelineFactory!();
        Assert.Equal(2, first.Middlewares!.Count);
        Assert.Same(preceding, first.Middlewares[0]);
        Assert.IsType<UoEncryptionMiddleware>(first.Middlewares[1]);
        Assert.NotSame(first.Middlewares[1], second.Middlewares![1]);
        Assert.IsType<UoEncryptionMiddleware>(Assert.Single(login.ConnectionPipelineFactory!().Middlewares!));
    }

    [Fact]
    public void Create_InvalidEnabledEncryptionFailsBeforeListenerCreation()
    {
        var config = new MoongateServerConfig();
        config.Network.Encryption.Mode = NetworkEncryptionMode.Required;
        Assert.Throws<InvalidOperationException>(() => UoNetworkOptionsFactory.CreateLogin(config, PacketRegistry.Default));
        Assert.Throws<InvalidOperationException>(() => UoNetworkOptionsFactory.CreateGame(config, PacketRegistry.Default, []));
    }

    private sealed class PassThroughMiddleware : INetMiddleware
    {
        public ValueTask<ReadOnlyMemory<byte>> ProcessAsync(
            MoongateTcpClient? client,
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default
        )
        {
            return ValueTask.FromResult(data);
        }
    }
}
