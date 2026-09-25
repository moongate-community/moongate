using System.Net;
using Moongate.Core.Utils;
using Moongate.Network.Interfaces.Framing;
using Moongate.Network.Packets.Registry;
using Moongate.Server.Core.Data.Network;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Network.Framing;

namespace Moongate.Server.Bootstrap.Internal;

internal static class UoNetworkOptionsFactory
{
    internal static NetworkListenerOptions CreateGame(MoongateServerConfig config)
    {
        return Create(config, config.Network.GamePort, () => new GameSeedFramer(PacketRegistry.Default));
    }

    internal static NetworkListenerOptions CreateLogin(MoongateServerConfig config)
    {
        return Create(config, config.Network.LoginPort, () => new UoPacketFramer(PacketRegistry.Default));
    }

    private static NetworkListenerOptions Create(MoongateServerConfig config, int port, Func<INetFramer> framerFactory)
    {
        var addresses = config.Network.ListenAddress == "0.0.0.0"
            ? NetworkUtils.GetLocalIpAddresses().ToArray()
            : new[] { IPAddress.Parse(config.Network.ListenAddress) };

        return new()
        {
            Endpoints = addresses.Select(address => new IPEndPoint(address, port)).ToArray(),
            ConnectionPipelineFactory = () => new() { Framer = framerFactory() }
        };
    }
}
