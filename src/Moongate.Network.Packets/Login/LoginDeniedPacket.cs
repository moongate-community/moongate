using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Login;

public sealed class LoginDeniedPacket : IOutgoingPacket
{
    private const byte PacketOpCode = 0x82;
    private const int PacketLength = 2;

    public byte OpCode => PacketOpCode;
    public int Length => PacketLength;
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
