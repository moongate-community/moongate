using DryIoc;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Packets;

namespace Moongate.Server.Bootstrap.Internal;

internal static class PacketPipelineRegistration
{
    internal static Container Register(Container container)
    {
        // Default-priority plugin dependencies start before handler binding and remain alive
        // until listeners, connection cleanup, and both packet services have stopped.
        return container.RegisterMoongateService<IConnectionService, ConnectionService>(40)
                        .RegisterMoongateService<IPacketSendService, PacketSendService>(50)
                        .RegisterMoongateService<IPacketDispatchService, PacketDispatchService>(60)
                        .RegisterMoongateService<INetworkService, NetworkService>(100);
    }
}
