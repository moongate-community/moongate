using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.Login;

namespace Moongate.Tests.Packets.Login;

public class SelectServerPacketTests
{
    [Test]
    public void SelectServerPacket_ShouldHaveOpCode0xA0()
    {
        var packet = new SelectServerPacket();
        Assert.That(packet.OpCode, Is.EqualTo(0xA0));
    }

    [Test]
    public void SelectServerPacket_ShouldDeserializeValidData()
    {
        var packet = new SelectServerPacket();
        using var writer = new SpanWriter(3);
        writer.Write((byte)0xA0);
        writer.Write((ushort)1); // Server index

        var result = packet.Read(writer.ToArray().AsMemory());
        Assert.That(result, Is.True);
    }

    [Test]
    public void SelectServerPacket_ShouldFailWithEmptyData()
    {
        var packet = new SelectServerPacket();
        var result = packet.Read(ReadOnlyMemory<byte>.Empty);
        Assert.That(result, Is.False);
    }

    [Test]
    public void SelectServerPacket_ShouldFailWithWrongOpCode()
    {
        var packet = new SelectServerPacket();
        using var writer = new SpanWriter(3);
        writer.Write((byte)0xFF);
        writer.Write((ushort)0);

        var result = packet.Read(writer.ToArray().AsMemory());
        Assert.That(result, Is.False);
    }

    [Test]
    public void SelectServerPacket_ShouldHandleZeroIndex()
    {
        var packet = new SelectServerPacket();
        using var writer = new SpanWriter(3);
        writer.Write((byte)0xA0);
        writer.Write((ushort)0);

        var result = packet.Read(writer.ToArray().AsMemory());
        Assert.That(result, Is.True);
    }

    [Test]
    public void SelectServerPacket_ShouldHandleMaxIndex()
    {
        var packet = new SelectServerPacket();
        using var writer = new SpanWriter(3);
        writer.Write((byte)0xA0);
        writer.Write((ushort)0xFFFF);

        var result = packet.Read(writer.ToArray().AsMemory());
        Assert.That(result, Is.True);
    }
}
