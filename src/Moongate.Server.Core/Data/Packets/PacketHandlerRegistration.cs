using DryIoc;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Core.Data.Packets;

/// <summary>Immutable handler metadata with a binder invoked once by dispatcher startup.</summary>
public sealed class PacketHandlerRegistration
{
    public Type PacketType { get; }
    public Type HandlerType { get; }
    public Func<IResolverContext, Action<GameSession, IPacket>> Bind { get; }

    public PacketHandlerRegistration(
        Type packetType,
        Type handlerType,
        Func<IResolverContext, Action<GameSession, IPacket>> bind
    )
    {
        PacketType = packetType;
        HandlerType = handlerType;
        Bind = bind;
    }
}
