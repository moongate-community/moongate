using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Tests.Support.Metadata;
using System.Reflection;

namespace Moongate.Network.Packets.Tests.Data.Packets;

public class PacketDescriptorTests
{
    [Fact]
    public void Descriptor_RepeatedBaseAndRegistryAccess_ReturnsSameInstance()
    {
        var first = PingPacket.Descriptor;
        var second = PingPacket.Descriptor;
        Assert.True(PacketRegistry.Default.TryGetDescriptor(first.OpCode, first.Direction & ~Moongate.Network.Packets.Types.Packets.PacketDirection.Outgoing, out var registered));

        Assert.Same(first, second);
        Assert.Same(first, registered);
    }

    [Theory]
    [InlineData(typeof(MissingMetadataPacket))]
    [InlineData(typeof(InvalidFixedLengthPacket))]
    [InlineData(typeof(InvalidVariableLengthPacket))]
    [InlineData(typeof(UnknownSizingPacket))]
    public void Descriptor_InvalidMetadata_ThrowsTypeSpecificConfigurationError(Type packetType)
    {
        var descriptorProperty = packetType.GetProperty(
            "Descriptor",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;

        var error = Assert.ThrowsAny<Exception>(() => descriptorProperty.GetValue(null));
        var details = error.ToString();
        Assert.Contains(packetType.FullName!, details, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", details, StringComparison.Ordinal);
    }

    [Fact]
    public void FixedBase_WithVariableMetadata_FailsClearly()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new VariableFixedBasePacket());

        Assert.Contains(typeof(VariableFixedBasePacket).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains("not fixed-size", error.Message, StringComparison.Ordinal);
    }
}
