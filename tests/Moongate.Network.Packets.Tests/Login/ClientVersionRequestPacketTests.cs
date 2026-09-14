using Moongate.Network.Packets.Login;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Tests.Support;

namespace Moongate.Network.Packets.Tests.Login;

public class ClientVersionRequestPacketTests
{
    [Fact]
    public void Encode_Query_MatchesThreeByteFixtureAndWritesAtomically()
    {
        var packet = new ClientVersionRequestPacket();
        var expected = Convert.FromHexString("BD0003");

        Assert.Equal(expected, PacketCodec.Encode(packet));
        PacketWriteAssertions.AssertAtomicFailure(packet);
        PacketWriteAssertions.AssertExactAndOversized(packet, expected);
    }
}
