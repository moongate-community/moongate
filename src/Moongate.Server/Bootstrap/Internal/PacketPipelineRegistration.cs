using DryIoc;
using Moongate.Server.Core.Data.Network;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Game;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Packets;

namespace Moongate.Server.Bootstrap.Internal;

internal static class PacketPipelineRegistration
{
    internal static Container Register(Container container)
    {
        container.RegisterDelegate<NetworkListenerOptions>(resolver => GameNetworkOptionsFactory.Create(resolver.Resolve<MoongateServerConfig>()), Reuse.Singleton);
        container.Register<INetworkService, NetworkService>(Reuse.Singleton);

        // Default-priority plugin dependencies start before handler binding and remain alive
        // until listeners, connection cleanup, and both packet services have stopped.
        return container.RegisterMoongateService<IConnectionService, ConnectionService>(40)
                        .RegisterMoongateService<IPacketSendService, PacketSendService>(50)
                        .RegisterMoongateService<IPacketDispatchService, PacketDispatchService>(60)
                        .RegisterMoongateService<IGameServerService, GameServerService>(100);
    }
}
