using System.Text;
using Moongate.Network.Packets.Incoming;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Incoming;

public class GameServerLoginPacketTests
{
    [Fact]
    public void Read_ParsesAuthKeyAccountAndPassword()
    {
        var buffer = new byte[65];
        buffer[0] = 0x91;
        buffer[1] = 0x11;
        buffer[2] = 0x22;
        buffer[3] = 0x33;
        buffer[4] = 0x44; // authKey big-endian
        Encoding.ASCII.GetBytes("squid").CopyTo(buffer.AsSpan(5));
        Encoding.ASCII.GetBytes("secret").CopyTo(buffer.AsSpan(35));

        var reader = new SpanReader(buffer);
        var packet = GameServerLoginPacket.Read(ref reader);

        Assert.Equal((byte)0x91, GameServerLoginPacket.PacketId);
        Assert.Equal(0x11223344u, packet.AuthKey);
        Assert.Equal("squid", packet.Account);
        Assert.Equal("secret", packet.Password);
    }
}
