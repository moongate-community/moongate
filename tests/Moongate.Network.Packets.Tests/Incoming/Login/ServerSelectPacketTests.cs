using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Serialization;

namespace Moongate.Network.Packets.Tests.Incoming.Login;

public class ServerSelectPacketTests
{
    [Fact]
    public void TryDecode_BigEndianIndex_ReadsUInt16()
    {
        Assert.True(PacketCodec.TryDecode<ServerSelectPacket>(Convert.FromHexString("A00102"), out var packet));
        Assert.Equal(0xA0, packet.OpCode);
        Assert.Equal(3, packet.Length);
        Assert.Equal((ushort)258, packet.ServerIndex);

        Assert.True(PacketCodec.TryDecode<ServerSelectPacket>(Convert.FromHexString("A01234"), out var regression));
        Assert.Equal((ushort)0x1234, regression.ServerIndex);
    }

    [Theory, InlineData(""), InlineData("A0"), InlineData("A001"), InlineData("A0010200"), InlineData("A10102")]
    public void TryDecode_InvalidFrame_ReturnsFalse(string hex)
        => Assert.False(PacketCodec.TryDecode<ServerSelectPacket>(Convert.FromHexString(hex), out _));
}
