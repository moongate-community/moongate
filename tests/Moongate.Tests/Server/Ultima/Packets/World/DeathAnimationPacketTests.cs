using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class DeathAnimationPacketTests
{
    [Fact]
    public void Encode_WritesTheMobileTheCorpseAndFourEmptyBytes()
    {
        var packet = new DeathAnimationPacket(new(0x00000384), new(0x40000010));

        Assert.Equal(Convert.FromHexString("AF000003844000001000000000"), PacketCodec.Encode(packet));
    }
}
