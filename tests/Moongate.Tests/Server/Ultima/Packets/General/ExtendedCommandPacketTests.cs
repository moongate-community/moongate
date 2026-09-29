using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class ExtendedCommandPacketTests
{
    [Fact]
    public void TryParse_ReadsTheSubcommandAndItsPayload()
    {
        Assert.True(ExtendedCommandPacket.TryParse(Convert.FromHexString("BF0009" + "0010" + "40000010"), out var packet));

        Assert.Equal((ushort)0x10, packet.Subcommand);
        Assert.Equal(Convert.FromHexString("40000010"), packet.Payload);
    }
}
