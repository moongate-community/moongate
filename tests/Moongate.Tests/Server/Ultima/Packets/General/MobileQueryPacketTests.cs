using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class MobileQueryPacketTests
{
    [Theory]
    [InlineData("34EDEDEDED0400000002", MobileQueryType.Status, 0x00000002u)]
    [InlineData("34EDEDEDED0500000100", MobileQueryType.Skills, 0x00000100u)]
    public void TryParse_ReadsWhatIsAskedAndTheMobile(string hex, MobileQueryType kind, uint target)
    {
        Assert.True(MobileQueryPacket.TryParse(Convert.FromHexString(hex), out var packet));

        Assert.Equal((kind, new Serial(target)), (packet.Kind, packet.Target));
    }

    [Theory]
    [InlineData("34EDEDEDED04000000")]
    [InlineData("35EDEDEDED0400000002")]
    public void TryParse_AShortOrForeignPacket_IsRefused(string hex)
    {
        Assert.False(MobileQueryPacket.TryParse(Convert.FromHexString(hex), out _));
    }
}
