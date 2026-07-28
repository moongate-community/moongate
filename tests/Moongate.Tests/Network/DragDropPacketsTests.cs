using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network;

public class DragDropPacketsTests
{
    [Fact]
    public void DropItemApprovedPacket_Write_EmitsOnlyTheOpcode()
    {
        var writer = new SpanWriter(4);
        new DropItemApprovedPacket().Write(ref writer);

        Assert.Equal(new byte[] { 0x29 }, writer.Span.ToArray());
    }

    [Fact]
    public void DropItemPacket_Read_ParsesTargetAndContainer()
    {
        // 0x08, serial 0x40000001, x 0x0064, y 0x00C8, z 0x05, grid 0x00, container 0x40000002
        var reader = new SpanReader(
            new byte[]
            {
                0x08, 0x40, 0x00, 0x00, 0x01, 0x00, 0x64, 0x00, 0xC8, 0x05, 0x00, 0x40, 0x00, 0x00, 0x02
            }
        );

        var packet = DropItemPacket.Read(ref reader);

        Assert.Equal((Serial)0x40000001, packet.Serial);
        Assert.Equal(100, packet.X);
        Assert.Equal(200, packet.Y);
        Assert.Equal(5, packet.Z);
        Assert.Equal((Serial)0x40000002, packet.Container);
    }

    [Fact]
    public void DropItemPacket_Read_GroundDropCarriesTheAllOnesContainer()
    {
        var reader = new SpanReader(
            new byte[]
            {
                0x08, 0x40, 0x00, 0x00, 0x01, 0x00, 0x64, 0x00, 0xC8, 0x05, 0x00, 0xFF, 0xFF, 0xFF, 0xFF
            }
        );

        var packet = DropItemPacket.Read(ref reader);

        Assert.Equal((Serial)0xFFFFFFFF, packet.Container);
    }

    [Fact]
    public void PickUpItemPacket_Read_ParsesSerialAndAmount()
    {
        var reader = new SpanReader(new byte[] { 0x07, 0x40, 0x00, 0x00, 0x01, 0x00, 0x05 });

        var packet = PickUpItemPacket.Read(ref reader);

        Assert.Equal((Serial)0x40000001, packet.Serial);
        Assert.Equal(5, packet.Amount);
    }
}
