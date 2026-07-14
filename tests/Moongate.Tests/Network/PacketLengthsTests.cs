using Moongate.Network.Protocol;

namespace Moongate.Tests.Network;

public class PacketLengthsTests
{
    [Fact]
    public void Count_MatchesNumberOfDocumentedPackets()
        => Assert.Equal(198, PacketLengths.Count);

    [Theory, InlineData(0xEF, 21), InlineData(0x80, 62), InlineData(0xA0, 3), InlineData(0x91, 65), InlineData(0x5D, 73),
     InlineData(0x02, 7), InlineData(0x22, 3), InlineData(0x73, 2), InlineData(0x82, 2), InlineData(0xF8, 106)]

    // login seed (ClassicUO)
    // account login request
    // select server
    // game login
    // character select
    // move request
    // movement ack
    // ping
    // login denied
     // character creation (7.x)
    public void Get_FixedLengthPackets_ReturnsKnownSize(int id, int expected)
        => Assert.Equal((short)expected, PacketLengths.Get((byte)id));

    [Fact]
    public void Get_UndefinedPacket_ReturnsUnknown()
        => Assert.Equal(PacketLengths.Unknown, PacketLengths.Get(0xFF));

    [Theory, InlineData(0xA8), InlineData(0xA9), InlineData(0xAD), InlineData(0xBD), InlineData(0xBF), InlineData(0xB1)]

    // server list
    // character list
    // unicode speech
    // client version
    // extended command
     // gump response
    public void Get_VariableLengthPackets_ReturnsVariable(int id)
        => Assert.Equal(PacketLengths.Variable, PacketLengths.Get((byte)id));
}
