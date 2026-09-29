using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Data.Tooltips;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class PropertyListPacketTests
{
    [Fact]
    public void Encode_WritesTheHeaderTheLinesAndTheTerminator()
    {
        var list = new PropertyList();
        list.Add(1042971, "Hi");

        var bytes = PacketCodec.Encode(new PropertyListPacket(new(0x40000010), list));

        var expected = "D6" + "001D" + "0001" + "40000010" + "0000" + list.Hash.ToString("X8") +
                       "000FEA1B" + "0004" + "48006900" + "00000000";
        Assert.Equal(Convert.FromHexString(expected), bytes);
    }

    [Fact]
    public void Info_WritesTheSerialAndTheHashWithBit30()
    {
        var list = new PropertyList();
        list.Add(1042971, "Hi");

        var bytes = PacketCodec.Encode(new PropertyListInfoPacket(new(0x40000010), list.Hash));

        Assert.Equal(Convert.FromHexString("DC" + "40000010" + (list.Hash | 0x40000000).ToString("X8")), bytes);
    }
}
