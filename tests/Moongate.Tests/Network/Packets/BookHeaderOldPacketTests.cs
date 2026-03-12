using Moongate.Network.Packets.Incoming.Books;
using Moongate.Network.Spans;

namespace Moongate.Tests.Network.Packets;

public class BookHeaderOldPacketTests
{
    [Test]
    public void TryParse_WithValidPayload_ShouldPopulateFields()
    {
        var source = new BookHeaderOldPacket
        {
            BookSerial = 0x40000010u,
            IsWritable = true,
            PageCount = 42,
            Title = "Travel Journal",
            Author = "Nakama"
        };

        var bytes = Write(source);
        var packet = new BookHeaderOldPacket();
        var ok = packet.TryParse(bytes);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(packet.BookSerial, Is.EqualTo(source.BookSerial));
                Assert.That(packet.IsWritable, Is.EqualTo(source.IsWritable));
                Assert.That(packet.PageCount, Is.EqualTo(source.PageCount));
                Assert.That(packet.Title, Is.EqualTo(source.Title));
                Assert.That(packet.Author, Is.EqualTo(source.Author));
            }
        );
    }

    [Test]
    public void Write_ShouldEmitFixedLengthPacket()
    {
        var packet = new BookHeaderOldPacket
        {
            BookSerial = 0x40000011u,
            IsWritable = false,
            PageCount = 12,
            Title = "Notes",
            Author = "Archivist"
        };

        var bytes = Write(packet);

        Assert.That(bytes.Length, Is.EqualTo(99));
        Assert.That(bytes[0], Is.EqualTo(0x93));
    }

    private static byte[] Write(BookHeaderOldPacket packet)
    {
        var writer = new SpanWriter(128, true);
        packet.Write(ref writer);
        var bytes = writer.ToArray();
        writer.Dispose();

        return bytes;
    }
}
