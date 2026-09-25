using DryIoc;
using Moongate.Server.Core.Data.Network;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Game;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Network.Middleware;
using Moongate.Server.Services.Packets;

namespace Moongate.Server.Bootstrap.Internal;

internal static class PacketPipelineRegistration
{
    internal static Container Register(Container container)
    {
        container.Register<UoCompressionMiddleware>(Reuse.Singleton);
        container.RegisterDelegate<NetworkListenerOptions>(
            resolver => UoNetworkOptionsFactory.CreateGame(
                resolver.Resolve<MoongateServerConfig>(),
                [resolver.Resolve<UoCompressionMiddleware>()]
            ),
            Reuse.Singleton
        );
        container.Register<INetworkService, NetworkService>(Reuse.Singleton);

        // Default-priority plugin dependencies start before handler binding and remain alive
        // until listeners, connection cleanup, and both packet services have stopped.
        return container.AddMoongateService<IConnectionService, ConnectionService>(40)
            .AddMoongateService<IPacketSendService, PacketSendService>(50)
            .AddMoongateService<IPacketDispatchService, PacketDispatchService>(60)
            .AddMoongateService<IGameServerService, GameServerService>(100);
    }
}
