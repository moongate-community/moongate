using System.Buffers.Binary;
using System.Text;
using Moongate.Network.Packets.Incoming;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Incoming;

public class CharacterSelectPacketTests
{
    [Fact]
    public void PacketId_Is0x5D()
        => Assert.Equal(0x5D, CharacterSelectPacket.PacketId);

    [Fact]
    public void Read_ParsesNameAndSlot()
    {
        var buffer = new byte[73];
        buffer[0] = 0x5D;
        Encoding.ASCII.GetBytes("Hero").CopyTo(buffer, 5);          // name field starts after the 4-byte pattern
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(65), 3); // char slot

        var reader = new SpanReader(buffer);
        var packet = CharacterSelectPacket.Read(ref reader);

        Assert.Equal("Hero", packet.Name.TrimEnd('\0'));
        Assert.Equal(3, packet.Slot);
    }
}
