using Moongate.Network.Packets.Incoming.Player;
using Moongate.Network.Spans;

namespace Moongate.Tests.Network.Packets;

public class ClientViewRangePacketTests
{
    [Test]
    public void TryParse_ShouldReadRange()
    {
        var packet = new ClientViewRangePacket();
        var parsed = packet.TryParse(new byte[] { 0xC8, 0x0A });

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.Range, Is.EqualTo(10));
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeOpcodeAndRange()
    {
        var packet = new ClientViewRangePacket(12);
        var writer = new SpanWriter(2, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        Assert.That(data, Is.EqualTo(new byte[] { 0xC8, 0x0C }));
    }
}
