using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Serialization;

namespace Moongate.Network.Packets.Tests.Incoming.Login;

public class ClientVersionPacketTests
{
    [Fact]
    public void TryDecode_WithOrWithoutFinalTerminator_PreservesActualFrameLength()
    {
        var withoutNull = Convert.FromHexString("BD000C372E302E3130392E30");
        var withNull = Convert.FromHexString("BD000D372E302E3130392E3000");

        Assert.True(PacketCodec.TryDecode<ClientVersionPacket>(withoutNull, out var canonical));
        Assert.Equal("7.0.109.0", canonical.Version);
        Assert.Equal(12, canonical.Length);
        Assert.True(PacketCodec.TryDecode<ClientVersionPacket>(withNull, out var compatible));
        Assert.Equal("7.0.109.0", compatible.Version);
        Assert.Equal(13, compatible.Length);
    }

    [Fact]
    public void TryDecode_EveryProperPrefixOfValidFrames_ReturnsFalse()
    {
        var fixtures = new[]
        {
            Convert.FromHexString("BD000C372E302E3130392E30"),
            Convert.FromHexString("BD000D372E302E3130392E3000")
        };

        foreach (var fixture in fixtures)
        {
            for (var length = 0; length < fixture.Length; length++)
            {
                Assert.False(PacketCodec.TryDecode<ClientVersionPacket>(fixture.AsSpan(0, length), out _));
            }
        }
    }

    [Theory,
     InlineData("BD0003"),
     InlineData("BD0000"),
     InlineData("BD0001"),
     InlineData("BD0002"),
     InlineData("BD000400"),
     InlineData("BD00044100"),
     InlineData("BD000541"),
     InlineData("BD0004414200"),
     InlineData("BD000541004200"),
     InlineData("BD000480"),
     InlineData("BC000441")]
    public void TryDecode_InvalidHeaderOrText_ReturnsFalse(string hex)
    {
        Assert.False(PacketCodec.TryDecode<ClientVersionPacket>(Convert.FromHexString(hex), out _));
    }

    [Theory, InlineData(""), InlineData("é"), InlineData("7\0.0")]
    public void Constructor_InvalidVersion_ThrowsArgumentException(string version)
    {
        Assert.Throws<ArgumentException>(() => new ClientVersionPacket(version));
    }
}
