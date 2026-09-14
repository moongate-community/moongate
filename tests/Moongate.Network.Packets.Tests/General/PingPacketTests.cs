using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Tests.Support;

namespace Moongate.Network.Packets.Tests.General;

public class PingPacketTests
{
    [Fact]
    public void TryDecode_KnownPingBytes_ReadsSequence()
    {
        Assert.True(PacketCodec.TryDecode<PingPacket>([0x73, 0x2A], out var packet));
        Assert.Equal((byte)42, packet.Sequence);
        Assert.Equal(2, packet.Length);
    }

    [Fact]
    public void Encode_Ping_MatchesWireFixture()
    {
        Assert.Equal(new byte[] { 0x73, 0x2A }, PacketCodec.Encode(new PingPacket(42)));
    }

    [Fact]
    public void Encode_MaxSequence_PreservesSequenceAndWritesAtomically()
    {
        var packet = new PingPacket(0xFF);
        var expected = Convert.FromHexString("73FF");

        Assert.Equal(expected, PacketCodec.Encode(packet));
        PacketWriteAssertions.AssertAtomicFailure(packet);
        PacketWriteAssertions.AssertExactAndOversized(packet, expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("73")]
    [InlineData("732A00")]
    [InlineData("742A")]
    public void TryDecode_InvalidCompleteFrame_ReturnsFalse(string hex)
    {
        Assert.False(PacketCodec.TryDecode<PingPacket>(Convert.FromHexString(hex), out _));
    }
}
