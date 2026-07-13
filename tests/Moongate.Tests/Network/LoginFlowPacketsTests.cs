using System.Net;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network;

public class LoginFlowPacketsTests
{
    [Fact]
    public void ServerListPacket_Write_HasHeaderFlagCountAndEntry()
    {
        var packet = new ServerListPacket("Moongate", IPAddress.Parse("127.0.0.1"));

        var writer = new SpanWriter(64);
        packet.Write(ref writer);
        var bytes = writer.Span.ToArray();

        Assert.Equal(0xA8, bytes[0]);
        Assert.Equal(46, (bytes[1] << 8) | bytes[2]); // total length, big-endian
        Assert.Equal(0x5D, bytes[3]);
        Assert.Equal(1, (bytes[4] << 8) | bytes[5]); // one server
        Assert.Equal(46, bytes.Length);
        // name ascii starts at offset 8 (after index ushort at 6..7)
        Assert.Equal("Moongate", System.Text.Encoding.ASCII.GetString(bytes, 8, 8));
        // IP at offset 42 (index 2 + name 32 + percentFull 1 + timezone 1), reversed: 1,0,0,127
        Assert.Equal(new byte[] { 1, 0, 0, 127 }, bytes.AsSpan(42, 4).ToArray());
    }

    [Fact]
    public void ConnectToGameServerPacket_Write_Is11BytesWithPortAndKey()
    {
        var packet = new ConnectToGameServerPacket(IPAddress.Parse("127.0.0.1"), 2593, 0xDEADBEEF);

        var writer = new SpanWriter(16);
        packet.Write(ref writer);
        var bytes = writer.Span.ToArray();

        Assert.Equal(11, bytes.Length);
        Assert.Equal(0x8C, bytes[0]);
        Assert.Equal(2593, (bytes[5] << 8) | bytes[6]);                       // port big-endian
        Assert.Equal(0xDEADBEEFu, ((uint)bytes[7] << 24) | ((uint)bytes[8] << 16) | ((uint)bytes[9] << 8) | bytes[10]);
    }

    [Fact]
    public void SelectServerPacket_Read_ParsesIndex()
    {
        var bytes = new byte[] { 0xA0, 0x00, 0x03 };

        var reader = new SpanReader(bytes);
        var packet = SelectServerPacket.Read(ref reader);

        Assert.Equal(3, packet.ShardIndex);
    }

    [Fact]
    public void ConnectToGameServer_WritesAddressInNormalOrder()
    {
        var packet = new ConnectToGameServerPacket(IPAddress.Parse("192.168.1.50"), 2593, 1);

        var writer = new SpanWriter(16);
        packet.Write(ref writer);
        var bytes = writer.Span.ToArray();

        // The 0x8C redirect writes the IP in normal order (unlike the 0xA8 server list,
        // which reverses it); otherwise the client dials a mangled address and cannot reconnect.
        Assert.Equal(new byte[] { 192, 168, 1, 50 }, bytes.AsSpan(1, 4).ToArray());
    }
}
