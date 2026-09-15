using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Serialization;

namespace Moongate.Network.Packets.Tests.Incoming.Login;

public class LoginSeedPacketTests
{
    private static readonly byte[] Fixture = Convert.FromHexString(
        "EF1234567800000007000000000000006D00000000");

    [Fact]
    public void TryDecode_KnownFixture_ReadsAllUnsignedFields()
    {
        Assert.True(PacketCodec.TryDecode<LoginSeedPacket>(Fixture, out var packet));
        Assert.Equal(0xEF, packet.OpCode);
        Assert.Equal(21, packet.Length);
        Assert.Equal(0x12345678u, packet.Seed);
        Assert.Equal(7u, packet.Major);
        Assert.Equal(0u, packet.Minor);
        Assert.Equal(109u, packet.Revision);
        Assert.Equal(0u, packet.Patch);

        var highBitFixture = Fixture.ToArray();
        highBitFixture[1] = 0x80;
        highBitFixture[2] = 0;
        highBitFixture[3] = 0;
        highBitFixture[4] = 0;
        Assert.True(PacketCodec.TryDecode<LoginSeedPacket>(highBitFixture, out var highBit));
        Assert.Equal(0x80000000u, highBit.Seed);
    }

    [Fact]
    public void TryDecode_IncompleteWrongOrAppendedFrame_ReturnsFalse()
    {
        for (var length = 0; length < Fixture.Length; length++)
        {
            Assert.False(PacketCodec.TryDecode<LoginSeedPacket>(Fixture.AsSpan(0, length), out _));
        }

        var wrongOpcode = Fixture.ToArray();
        wrongOpcode[0] = 0xEE;
        Assert.False(PacketCodec.TryDecode<LoginSeedPacket>(wrongOpcode, out _));
        Assert.False(PacketCodec.TryDecode<LoginSeedPacket>([.. Fixture, 0x00], out _));
    }
}
