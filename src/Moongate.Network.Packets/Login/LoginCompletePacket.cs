using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Login;

public sealed class LoginCompletePacket : IOutgoingPacket
{
    private const byte PacketOpCode = 0x55;
    private const int PacketLength = 1;

    public byte OpCode => PacketOpCode;
    public int Length => PacketLength;

    public LoginCompletePacket()
    {
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
    }
}
