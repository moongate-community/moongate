using System.Text;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public sealed class DisplayCharacterProfilePacketTests
{
    [Test]
    public void Write_ShouldEmitModernUoCompatibleProfilePayload()
    {
        var packet = new DisplayCharacterProfilePacket(
            (Serial)0x00000001u,
            "Lord Marcus",
            "This account is 2 days old.",
            "Hello"
        );
        var writer = new SpanWriter(256, true);
        packet.Write(ref writer);
        var bytes = writer.ToArray();
        writer.Dispose();

        Assert.Multiple(
            () =>
            {
                Assert.That(bytes[0], Is.EqualTo(0xB8));
                Assert.That((bytes[1] << 8) | bytes[2], Is.EqualTo(bytes.Length));
                Assert.That(ToUInt32(bytes, 3), Is.EqualTo(0x00000001u));
                Assert.That(ReadAsciiNull(bytes, 7), Is.EqualTo("Lord Marcus"));

                var footerOffset = 7 + "Lord Marcus".Length + 1;
                var footer = ReadBigEndianUnicodeNull(bytes, footerOffset, out var bodyOffset);

                Assert.That(footer, Is.EqualTo("This account is 2 days old."));
                Assert.That(ReadBigEndianUnicodeNull(bytes, bodyOffset, out _), Is.EqualTo("Hello"));
            }
        );
    }

    [Test]
    public void Write_WhenStringsAreNull_ShouldEmitEmptyNullTerminatedFields()
    {
        var packet = new DisplayCharacterProfilePacket((Serial)0x00000002u, null, null, null);
        var writer = new SpanWriter(128, true);
        packet.Write(ref writer);
        var bytes = writer.ToArray();
        writer.Dispose();

        Assert.Multiple(
            () =>
            {
                Assert.That(bytes[0], Is.EqualTo(0xB8));
                Assert.That((bytes[1] << 8) | bytes[2], Is.EqualTo(bytes.Length));
                Assert.That(ToUInt32(bytes, 3), Is.EqualTo(0x00000002u));
                Assert.That(bytes[7], Is.EqualTo(0x00));
                Assert.That(bytes[8], Is.EqualTo(0x00));
                Assert.That(bytes[9], Is.EqualTo(0x00));
                Assert.That(bytes[10], Is.EqualTo(0x00));
                Assert.That(bytes[11], Is.EqualTo(0x00));
            }
        );
    }

    private static string ReadAsciiNull(byte[] bytes, int offset)
    {
        var end = offset;

        while (end < bytes.Length && bytes[end] != 0x00)
        {
            end++;
        }

        return Encoding.ASCII.GetString(bytes, offset, end - offset);
    }

    private static string ReadBigEndianUnicodeNull(byte[] bytes, int offset, out int nextOffset)
    {
        var end = offset;

        while (end + 1 < bytes.Length)
        {
            if (bytes[end] == 0x00 && bytes[end + 1] == 0x00)
            {
                break;
            }

            end += 2;
        }

        var value = end == offset
                        ? string.Empty
                        : Encoding.BigEndianUnicode.GetString(bytes, offset, end - offset);

        nextOffset = end + 2;

        return value;
    }

    private static uint ToUInt32(byte[] bytes, int offset)
        => (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);
}
