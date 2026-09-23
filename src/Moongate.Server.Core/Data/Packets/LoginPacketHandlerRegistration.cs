using DryIoc;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Core.Data.Packets;

public sealed class LoginPacketHandlerRegistration
{
    public Type PacketType { get; }

    public Func<IResolverContext, Func<LoginSession, IPacket, CancellationToken, ValueTask>> Bind { get; }

    public LoginPacketHandlerRegistration(Type packetType,
        Func<IResolverContext, Func<LoginSession, IPacket, CancellationToken, ValueTask>> bind)
    {
        PacketType = packetType;
        Bind = bind;
    }
}
