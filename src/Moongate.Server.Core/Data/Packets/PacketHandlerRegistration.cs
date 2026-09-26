using DryIoc;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Packets;

namespace Moongate.Server.Core.Data.Packets;

/// <summary>
///     Immutable handler metadata with a binder invoked once by dispatcher startup.
/// </summary>
public sealed class PacketHandlerRegistration
{
    public Type PacketType { get; }
    public Type HandlerType { get; }
    public bool IsAsync { get; }
    public Func<IResolverContext, Action<GameSession, IPacket>> Bind { get; }
    public Func<IResolverContext, Func<PacketContext, IPacket, CancellationToken, ValueTask>> BindAsync { get; }

    public PacketHandlerRegistration(
        Type packetType,
        Type handlerType,
        Func<IResolverContext, Action<GameSession, IPacket>> bind
    )
    {
        PacketType = packetType;
        HandlerType = handlerType;
        Bind = bind;
        BindAsync = _ => throw new InvalidOperationException("This is a synchronous packet handler.");
    }

    public PacketHandlerRegistration(
        Type packetType,
        Type handlerType,
        Func<IResolverContext, Func<PacketContext, IPacket, CancellationToken, ValueTask>> bindAsync
    )
    {
        PacketType = packetType;
        HandlerType = handlerType;
        IsAsync = true;
        Bind = _ => throw new InvalidOperationException("This is an asynchronous packet handler.");
        BindAsync = bindAsync;
    }
}
