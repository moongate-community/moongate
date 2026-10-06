using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.BulletinBoards;

namespace Moongate.Tests.Server.Ultima.Packets.BulletinBoards;

public sealed class BulletinBoardDisplayPacketTests
{
    private static readonly Serial Board = new(0x40000001);

    [Fact]
    public void Encode_WritesTheBoardAndItsName_InThirtyEightBytes()
    {
        var bytes = PacketCodec.Encode(new BulletinBoardDisplayPacket(Board, "bulletin board"));

        Assert.Equal(
            Convert.FromHexString(
                "710026" + "00" + "40000001" + Convert.ToHexString(Encoding.ASCII.GetBytes("bulletin board")) +
                new string('0', 32)
            ),
            bytes
        );
        Assert.Equal(38, bytes.Length);
    }

    [Fact]
    public void Encode_ANameOfTwentyNineBytes_FillsTheFieldButItsLastByte()
    {
        var name = new string('a', 29);

        var bytes = PacketCodec.Encode(new BulletinBoardDisplayPacket(Board, name));

        Assert.Equal(38, bytes.Length);
        Assert.Equal(name, Encoding.ASCII.GetString(bytes, 8, 29));
        Assert.Equal(0, bytes[37]);
    }

    [Fact]
    public void Encode_ALongName_IsCutAtTwentyNineBytes()
    {
        var bytes = PacketCodec.Encode(new BulletinBoardDisplayPacket(Board, new string('a', 40)));

        Assert.Equal(38, bytes.Length);
        Assert.Equal(new string('a', 29), Encoding.ASCII.GetString(bytes, 8, 29));
        Assert.Equal(0, bytes[37]);
    }

    // "è" is two bytes: at the cut it goes whole, never its first half.
    [Fact]
    public void Encode_ACharacterOfSeveralBytesAtTheCut_IsLeftOutWhole()
    {
        var bytes = PacketCodec.Encode(new BulletinBoardDisplayPacket(Board, new string('a', 28) + "è"));

        Assert.Equal(38, bytes.Length);
        Assert.Equal(new string('a', 28), Encoding.ASCII.GetString(bytes, 8, 28));
        Assert.Equal([0, 0], bytes[36..38]);
    }
}
