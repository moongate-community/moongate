using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Outgoing.Login;

[PacketHandler(0x55, PacketSizing.Fixed, Length = 1)]
public sealed class LoginCompletePacket : BaseFixedPacket<LoginCompletePacket>, IOutgoingPacket
{
    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
    }
}
