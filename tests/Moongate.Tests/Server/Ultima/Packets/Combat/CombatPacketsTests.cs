using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.Combat;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.Combat;

public sealed class CombatPacketsTests
{
    [Fact]
    public void Combatant_WritesTheSerialOfTheTarget_OrZeroForNone()
    {
        Assert.Equal(
            new byte[] { 0xAA, 0x00, 0x00, 0x01, 0x02 },
            PacketCodec.Encode(new CombatantPacket(new Serial(0x102)))
        );
        Assert.Equal(new byte[] { 0xAA, 0x00, 0x00, 0x00, 0x00 }, PacketCodec.Encode(new CombatantPacket(Serial.Zero)));
    }

    [Fact]
    public void Swing_WritesTheFlagTheAttackerAndTheDefender()
    {
        var packet = new SwingPacket(new Serial(0x00000002), new Serial(0x00000300));

        Assert.Equal(
            new byte[] { 0x2F, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x03, 0x00 },
            PacketCodec.Encode(packet)
        );
    }

    [Theory]
    [InlineData(7, new byte[] { 0x0B, 0x00, 0x00, 0x03, 0x00, 0x00, 0x07 })]
    [InlineData(65535, new byte[] { 0x0B, 0x00, 0x00, 0x03, 0x00, 0xFF, 0xFF })]
    [InlineData(70000, new byte[] { 0x0B, 0x00, 0x00, 0x03, 0x00, 0xFF, 0xFF })]
    [InlineData(-3, new byte[] { 0x0B, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00 })]
    public void Damage_WritesTheSerialAndTheDamageKeptInAShort(int damage, byte[] expected)
    {
        Assert.Equal(expected, PacketCodec.Encode(new DamagePacket(new Serial(0x300), damage)));
    }

    [Fact]
    public void AttackRequest_ReadsTheSerialOfTheTarget()
    {
        Assert.True(AttackRequestPacket.TryParse(Convert.FromHexString("0500000307"), out var packet));

        Assert.Equal(new Serial(0x307), packet.Target);
    }

    [Fact]
    public void AttackRequest_TooShortOrOfAnotherOpcode_Fails()
    {
        Assert.False(AttackRequestPacket.TryParse(Convert.FromHexString("05000003"), out _));
        Assert.False(AttackRequestPacket.TryParse(Convert.FromHexString("0600000307"), out _));
    }
}
