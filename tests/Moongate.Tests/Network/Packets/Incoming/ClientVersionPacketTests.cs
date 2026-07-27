using System.Buffers.Binary;
using System.Text;
using Moongate.Network.Packets.Incoming;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Incoming;

public class ClientVersionPacketTests
{
    [Fact]
    public void PacketId_Is0xBD()
        => Assert.Equal(0xBD, ClientVersionPacket.PacketId);

    [Fact]
    public void Read_EmptyPayload_ReturnsEmptyVersion()
    {
        var buffer = new byte[] { 0xBD, 0x00, 0x03 };

        var reader = new SpanReader(buffer);
        var packet = ClientVersionPacket.Read(ref reader);

        Assert.Equal(string.Empty, packet.Version);
    }

    [Fact]
    public void Read_ParsesVersionString()
    {
        var version = "7.0.115.0\0";
        var buffer = new byte[3 + version.Length];
        buffer[0] = 0xBD;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(1), (ushort)buffer.Length);
        Encoding.ASCII.GetBytes(version).CopyTo(buffer, 3);

        var reader = new SpanReader(buffer);
        var packet = ClientVersionPacket.Read(ref reader);

        Assert.Equal("7.0.115.0", packet.Version);
    }
}
