using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Packets.Gumps;

namespace Moongate.Tests.Server.Ultima.Packets.Gumps;

public sealed class GumpPacketTests
{
    [Fact]
    public void Encode_WritesTheLayoutAndTheStrings()
    {
        var built = new GumpLayout().Add(new GumpPage()).Add(new GumpText { Text = "Hi" }).Build();

        var bytes = PacketCodec.Encode(new GumpPacket(1, 2, 10, 20, built));

        // "{ page 0 }{ text 0 0 0 0 }" and its NUL: 27 bytes; one string of 2 UTF-16 characters.
        Assert.Equal(
            Convert.FromHexString(
                "B00038" + "00000001" + "00000002" + "0000000A" + "00000014" + "001B" +
                Convert.ToHexString("{ page 0 }{ text 0 0 0 0 }\0"u8) + "0001" + "0002" + "00480069"
            ),
            bytes
        );
    }

    [Fact]
    public void Encode_WithNoStrings_WritesAnEmptyTable()
    {
        var built = new GumpLayout().Add(new GumpPage()).Build();

        var bytes = PacketCodec.Encode(new GumpPacket(1, 2, 0, 0, built));

        Assert.Equal("0000", Convert.ToHexString(bytes[^2..]));
    }
}
