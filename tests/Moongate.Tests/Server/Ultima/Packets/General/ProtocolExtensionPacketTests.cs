using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class ProtocolExtensionPacketTests
{
    [Fact]
    public void TryParse_APartyLocationsQuery_KeepsItsLength()
    {
        // Opcode, length 4, command 0 (where are the members of my party).
        Assert.True(ProtocolExtensionPacket.TryParse(Convert.FromHexString("F0" + "0004" + "00"), out var packet));

        Assert.Equal(4, packet.Length);
    }

    [Fact]
    public void TryParse_AGuildLocationsQuery_KeepsItsLength()
    {
        // Opcode, length 5, command 1 (where are the members of my guild), 1 asks for their places too.
        Assert.True(ProtocolExtensionPacket.TryParse(Convert.FromHexString("F0" + "0005" + "01" + "01"), out var packet));

        Assert.Equal(5, packet.Length);
    }

    [Fact]
    public void TryParse_AnotherOpcode_IsRefused()
    {
        Assert.False(ProtocolExtensionPacket.TryParse(Convert.FromHexString("F1" + "0004" + "00"), out _));
    }
}
