using System.Net;
using Moongate.Core.Utils;
using Moongate.Network.Interfaces.Framing;
using Moongate.Network.Interfaces.Middleware;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Data.Encryption;
using Moongate.Server.Core.Data.Network;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Network.Framing;
using Moongate.Server.Services.Network.Middleware;
using Moongate.Server.Types.Network;

namespace Moongate.Server.Bootstrap.Internal;

internal static class UoNetworkOptionsFactory
{
    internal static NetworkListenerOptions CreateGame(
        MoongateServerConfig config,
        PacketRegistry packets,
        IReadOnlyList<INetMiddleware> middlewares
    )
    {
        return Create(config, config.Network.GamePort, () => new GameSeedFramer(packets), true, middlewares);
    }

    internal static NetworkListenerOptions CreateLogin(MoongateServerConfig config, PacketRegistry packets)
    {
        return Create(config, config.Network.LoginPort, () => new UoPacketFramer(packets), false);
    }

    private static NetworkListenerOptions Create(
        MoongateServerConfig config,
        int port,
        Func<INetFramer> framerFactory,
        bool gameConnection,
        IReadOnlyList<INetMiddleware>? middlewares = null
    )
    {
        var encryption = config.Network.Encryption ??
                         throw new InvalidOperationException("network.encryption cannot be null.");
        encryption.Validate();
        var mode = encryption.Mode;
        var profile = mode == NetworkEncryptionMode.Disabled ? null : UoEncryptionProfile.Parse(encryption.ClientVersion);
        var configuredMiddlewares = middlewares?.ToArray();
        var addresses = config.Network.ListenAddress == "0.0.0.0"
            ? NetworkUtils.GetLocalIpAddresses().ToArray()
            : new[] { IPAddress.Parse(config.Network.ListenAddress) };

        return new()
        {
            Endpoints = addresses.Select(address => new IPEndPoint(address, port)).ToArray(),
            ConnectionPipelineFactory = () => new()
            {
                Framer = framerFactory(),
                Middlewares = profile is null
                    ? configuredMiddlewares
                    : [.. configuredMiddlewares ?? [], new UoEncryptionMiddleware(mode, profile, gameConnection)]
            }
        };
    }
}
