using Moongate.Network.Handlers;

namespace Moongate.Tests.Network;

public class PacketHandlerRegistryTests
{
    [Fact]
    public void TryDispatch_RegisteredHandler_ReceivesWholePacket()
    {
        var registry = new PacketHandlerRegistry();
        byte[]? received = null;

        registry.Register(0x02, packet => received = packet.ToArray());

        var frame = new byte[] { 0x02, 0x01, 0x05, 0x00, 0x00, 0x00, 0x00 };

        Assert.True(registry.TryDispatch(frame));
        Assert.Equal(frame, received);
    }

    [Fact]
    public void TryDispatch_NoHandler_ReturnsFalse()
    {
        Assert.False(new PacketHandlerRegistry().TryDispatch([0x73, 0x00]));
    }

    [Fact]
    public void TryDispatch_EmptyPacket_ReturnsFalse()
    {
        var registry = new PacketHandlerRegistry();
        registry.Register(0x02, _ => { });

        Assert.False(registry.TryDispatch([]));
    }

    [Fact]
    public void Register_DuplicateId_Throws()
    {
        var registry = new PacketHandlerRegistry();
        registry.Register(0x02, _ => { });

        Assert.Throws<InvalidOperationException>(() => registry.Register(0x02, _ => { }));
    }

    [Fact]
    public void Register_NullHandler_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PacketHandlerRegistry().Register(0x02, null!));
    }
}
