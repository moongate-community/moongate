using Moongate.Network.Packets.Incoming;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets;

/// <summary>
/// Drop-wear (0x13): what the client sends when an item on the cursor is dropped onto a paperdoll.
/// Ten bytes fixed — opcode, item, layer, and who to put it on.
/// </summary>
public class DropWearItemPacketTests
{
    [Fact]
    public void Read_TakesTheItemTheLayerAndTheWearer()
    {
        byte[] wire =
        [
            0x13,
            0x40, 0x00, 0x00, 0x0A,
            0x05,
            0x00, 0x00, 0x00, 0x01
        ];

        var reader = new SpanReader(wire);
        var packet = DropWearItemPacket.Read(ref reader);

        Assert.Equal(0x4000000Au, packet.Serial.Value);
        Assert.Equal(5, packet.Layer);
        Assert.Equal(0x00000001u, packet.Mobile.Value);
    }

    [Fact]
    public void PacketId_Is0x13()
        => Assert.Equal(0x13, DropWearItemPacket.PacketId);
}
