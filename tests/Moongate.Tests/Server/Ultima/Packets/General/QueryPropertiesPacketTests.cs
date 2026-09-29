using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class QueryPropertiesPacketTests
{
    [Fact]
    public void TryParse_ReadsEverySerial()
    {
        Assert.True(QueryPropertiesPacket.TryParse(Convert.FromHexString("D6000B" + "40000010" + "00000002"), out var packet));

        Assert.Equal([new Serial(0x40000010), new Serial(0x00000002)], packet.Serials);
    }

    [Fact]
    public void TryParse_ALengthThatIsNotWholeSerials_IsRefused()
    {
        Assert.False(QueryPropertiesPacket.TryParse(Convert.FromHexString("D60009" + "400000" + "1000"), out _));
    }

    [Fact]
    public void TryParse_MoreThan500Serials_IsRefused()
    {
        var length = 3 + 501 * 4;
        var data = new byte[length];
        data[0] = 0xD6;
        data[1] = (byte)(length >> 8);
        data[2] = (byte)length;

        Assert.False(QueryPropertiesPacket.TryParse(data, out _));
    }
}
