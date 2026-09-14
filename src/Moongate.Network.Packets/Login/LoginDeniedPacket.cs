using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Login;

[PacketHandler(0x82, PacketSizing.Fixed, Length = 2)]
public sealed class LoginDeniedPacket : BaseFixedPacket<LoginDeniedPacket>, IOutgoingPacket
{
    public byte Reason { get; }

    public LoginDeniedPacket(byte reason)
    {
        Reason = reason;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte(Reason);
    }
}
