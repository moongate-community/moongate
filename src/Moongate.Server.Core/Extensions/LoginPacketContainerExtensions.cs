using DryIoc;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Packets;

namespace Moongate.Server.Core.Extensions;

public static class LoginPacketContainerExtensions
{
    public static Container RegisterLoginPacketHandler<TPacket, THandler>(this Container container)
        where TPacket : class, IIncomingPacket<TPacket>
        where THandler : class, ILoginPacketHandler<TPacket>
    {
        if (!container.IsRegistered<LoginPacketHandlerRegistry>())
        {
            container.RegisterInstance(new LoginPacketHandlerRegistry());
        }

        container.Resolve<LoginPacketHandlerRegistry>().Register<TPacket, THandler>(container);
        return container;
    }
}
