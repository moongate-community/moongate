using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Tests.Support;

internal sealed class InvalidLengthPacket : IOutgoingPacket
{
    public byte OpCode => 0x01;
    public int Length { get; }

    public InvalidLengthPacket(int length)
    {
        Length = length;
    }

    public void Write(ref PacketWriter writer)
        => throw new InvalidOperationException("An invalid declared length must be rejected before writing.");
}
