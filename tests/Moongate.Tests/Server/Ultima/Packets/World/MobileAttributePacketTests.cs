using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class MobileAttributePacketTests
{
    private static readonly Serial Aria = new(0x00000002);

    [Fact]
    public void Hits_WriteTheMaximumThenTheCurrentValue()
    {
        Assert.Equal(Convert.FromHexString("A100000002003C0037"), PacketCodec.Encode(new MobileHitsPacket(Aria, 55, 60)));
    }

    // What the others see: a share of 100, never the real numbers.
    [Theory]
    [InlineData(55, 60, "A1000000020064005B")]
    [InlineData(0, 60, "A10000000200640000")]
    [InlineData(60, 60, "A10000000200640064")]
    [InlineData(5, 0, "A10000000200000005")]
    public void Hits_Normalized_AreAShareOfAHundred(int hits, int max, string expected)
    {
        Assert.Equal(Convert.FromHexString(expected), PacketCodec.Encode(new MobileHitsPacket(Aria, hits, max, true)));
    }

    [Fact]
    public void Mana_WritesTheMaximumThenTheCurrentValue()
    {
        Assert.Equal(Convert.FromHexString("A200000002000A0009"), PacketCodec.Encode(new MobileManaPacket(Aria, 9, 10)));
    }

    [Fact]
    public void Stamina_WritesTheMaximumThenTheCurrentValue()
    {
        Assert.Equal(Convert.FromHexString("A3000000020014000C"), PacketCodec.Encode(new MobileStaminaPacket(Aria, 12, 20)));
    }
}
