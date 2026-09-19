using System.Net;
using Moongate.Core.Utils;
using Moongate.Network.Data;
using Moongate.Network.Packets.Registry;
using Moongate.Server.Core.Data.Network;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Network.Framing;

namespace Moongate.Server.Bootstrap.Internal;

internal static class GameNetworkOptionsFactory
{
    internal static NetworkListenerOptions Create(MoongateServerConfig config)
    {
        var addresses = config.Network.ListenAddress == "0.0.0.0"
            ? NetworkUtils.GetLocalIpAddresses().ToArray()
            : new[] { IPAddress.Parse(config.Network.ListenAddress) };
        return new NetworkListenerOptions
        {
            Endpoints = addresses.Select(address => new IPEndPoint(address, config.Network.GamePort)).ToArray(),
            ConnectionPipelineFactory = () => new ConnectionPipeline { Framer = new UoPacketFramer(PacketRegistry.Default) }
        };
    }
}
