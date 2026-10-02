using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class ProfileRequestPacketTests
{
    [Fact]
    public void TryParse_ADisplayRequest_KeepsItsLength()
    {
        // Opcode, length 8, mode 0 (display), the serial of the character whose profile is asked for.
        Assert.True(ProfileRequestPacket.TryParse(Convert.FromHexString("B8" + "0008" + "00" + "000070F2"), out var packet));

        Assert.Equal(8, packet.Length);
    }

    [Fact]
    public void TryParse_AnotherOpcode_IsRefused()
    {
        Assert.False(ProfileRequestPacket.TryParse(Convert.FromHexString("B9" + "0008" + "00" + "000070F2"), out _));
    }
}
