using DryIoc;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Handlers.General;
using Moongate.Server.Handlers.Login;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Packets;

namespace Moongate.Server.Bootstrap.Internal;

internal static class PacketPipelineRegistration
{
    internal static Container Register(Container container)
    {
        // Default-priority plugin dependencies start before handler binding and remain alive
        // until listeners, connection cleanup, and both packet services have stopped.
        container.RegisterPacketHandler<PingPacket, PingPacketHandler>();
        container.RegisterPacketHandler<ClientVersionPacket, ClientVersionPacketHandler>();
        return container.RegisterMoongateService<IPacketSendService, PacketSendService>(50)
                        .RegisterMoongateService<IPacketDispatchService, PacketDispatchService>(60)
                        .RegisterMoongateService<INetworkService, NetworkService>(100);
    }
}
