using Moongate.Network.Protocol;

namespace Moongate.Tests.Network;

public class PacketLengthsTests
{
    [Theory]
    [InlineData(0xEF, 21)]  // login seed (ClassicUO)
    [InlineData(0x80, 62)]  // account login request
    [InlineData(0xA0, 3)]   // select server
    [InlineData(0x91, 65)]  // game login
    [InlineData(0x5D, 73)]  // character select
    [InlineData(0x02, 7)]   // move request
    [InlineData(0x22, 3)]   // movement ack
    [InlineData(0x73, 2)]   // ping
    [InlineData(0x82, 2)]   // login denied
    [InlineData(0xF8, 106)] // character creation (7.x)
    public void Get_FixedLengthPackets_ReturnsKnownSize(int id, int expected)
    {
        Assert.Equal((short)expected, PacketLengths.Get((byte)id));
    }

    [Theory]
    [InlineData(0xA8)] // server list
    [InlineData(0xA9)] // character list
    [InlineData(0xAD)] // unicode speech
    [InlineData(0xBD)] // client version
    [InlineData(0xBF)] // extended command
    [InlineData(0xB1)] // gump response
    public void Get_VariableLengthPackets_ReturnsVariable(int id)
    {
        Assert.Equal(PacketLengths.Variable, PacketLengths.Get((byte)id));
    }

    [Fact]
    public void Get_UndefinedPacket_ReturnsUnknown()
    {
        Assert.Equal(PacketLengths.Unknown, PacketLengths.Get(0xFF));
    }

    [Fact]
    public void Count_MatchesNumberOfDocumentedPackets()
    {
        Assert.Equal(198, PacketLengths.Count);
    }
}
