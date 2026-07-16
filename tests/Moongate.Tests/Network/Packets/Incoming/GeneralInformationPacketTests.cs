using System.Buffers.Binary;
using Moongate.Network.Packets.Incoming;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Incoming;

public class GeneralInformationPacketTests
{
    [Fact]
    public void Read_ParsesSubCommandAndPayload()
    {
        // 0xBF, length 9, sub-command 0x0005, then 4 payload bytes.
        var buffer = new byte[] { 0xBF, 0x00, 0x09, 0x00, 0x05, 0x01, 0x02, 0x03, 0x04 };

        var reader = new SpanReader(buffer);
        var packet = GeneralInformationPacket.Read(ref reader);

        Assert.Equal(0x0005, packet.SubCommand);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, packet.Payload);
    }

    [Fact]
    public void Read_NoPayload_ReturnsEmpty()
    {
        var buffer = new byte[] { 0xBF, 0x00, 0x05, 0x00, 0x0F };

        var reader = new SpanReader(buffer);
        var packet = GeneralInformationPacket.Read(ref reader);

        Assert.Equal(0x000F, packet.SubCommand);
        Assert.Empty(packet.Payload);
    }

    [Fact]
    public void PacketId_Is0xBF()
        => Assert.Equal(0xBF, GeneralInformationPacket.PacketId);
}
