using Moongate.Core.Types.Expansions;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Tests.Support;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Tests.Outgoing.Login;

public class SupportFeaturesPacketTests
{
    [Theory]
    [InlineData(FeatureFlags.None, "B900000000")]
    [InlineData(FeatureFlags.T2A | FeatureFlags.Aos | FeatureFlags.Sa | FeatureFlags.Ej, "B900810011")]
    [InlineData((FeatureFlags)0x80000001u, "B980000001")]
    [InlineData((FeatureFlags)uint.MaxValue, "B9FFFFFFFF")]
    public void Encode_Flags_UsesFiveByteBigEndianLayout(FeatureFlags flags, string hex)
    {
        var packet = new SupportFeaturesPacket(flags);
        var expected = Convert.FromHexString(hex);

        Assert.Equal(expected, PacketCodec.Encode(packet));
        PacketWriteAssertions.AssertAtomicFailure(packet);
        PacketWriteAssertions.AssertExactAndOversized(packet, expected);
    }

    [Fact]
    public void Encode_ExpansionPreset_CombinesLegacyFeatureBits()
    {
        var packet = new SupportFeaturesPacket(FeatureFlags.ExpansionEj);
        Assert.Equal(Convert.FromHexString("B900FF82D8"), PacketCodec.Encode(packet));
    }

    [Fact]
    public void Write_AfterExistingPayload_AppendsWithoutOverwritingPrefix()
    {
        Span<byte> destination = stackalloc byte[6];
        var writer = new PacketWriter(destination);
        writer.WriteByte(0xCC);
        new SupportFeaturesPacket(FeatureFlags.Sa).Write(ref writer);

        Assert.Equal(Convert.FromHexString("CCB900010000"), writer.WrittenSpan.ToArray());
        Assert.Equal(6, writer.WrittenCount);
    }

    [Fact]
    public void Registry_BuiltInPacket_IsFixedLengthAndOutgoingOnly()
    {
        var registry = PacketRegistry.Default;
        Assert.True(registry.TryGetDescriptor(0xB9, PacketDirection.Outgoing, out var descriptor));
        Assert.Equal(typeof(SupportFeaturesPacket), descriptor.PacketType);
        Assert.Equal(PacketSizing.Fixed, descriptor.Sizing);
        Assert.Equal(5, descriptor.FixedLength);
        Assert.False(registry.TryGetDescriptor(0xB9, PacketDirection.Incoming, out _));
        Assert.False(registry.TryDecode(Convert.FromHexString("B900000000"), out _));
    }
}
