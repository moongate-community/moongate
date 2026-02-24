using System.Buffers.Binary;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Network.Spans;

namespace Moongate.Tests.Network.Packets;

public class GenericGumpPacketTests
{
    [Test]
    public void WriteAndParse_ShouldRoundtripGenericGumpData()
    {
        var original = new GenericGumpPacket
        {
            SenderSerial = 0x00000002,
            GumpId = 0x000001CE,
            X = 250,
            Y = 300,
            Layout = "{ page 0 } { resizepic 0 0 5054 260 180 }"
        };
        original.TextLines.Add("first");
        original.TextLines.Add("second");
        original.TextLines.Add("third");

        var bytes = Write(original);

        var parsed = new GenericGumpPacket();
        var ok = parsed.TryParse(bytes);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(parsed.SenderSerial, Is.EqualTo(original.SenderSerial));
                Assert.That(parsed.GumpId, Is.EqualTo(original.GumpId));
                Assert.That(parsed.X, Is.EqualTo(original.X));
                Assert.That(parsed.Y, Is.EqualTo(original.Y));
                Assert.That(parsed.Layout, Is.EqualTo(original.Layout));
                Assert.That(parsed.TextLines, Is.EqualTo(original.TextLines));
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeHeaderAndSections()
    {
        var packet = new GenericGumpPacket
        {
            SenderSerial = 0x00000001,
            GumpId = 0x000001CD,
            X = 120,
            Y = 80,
            Layout = "{ nomove }{ noclose }"
        };
        packet.TextLines.Add("line one");
        packet.TextLines.Add("line two");

        var bytes = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(bytes[0], Is.EqualTo(0xB0));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1, 2)), Is.EqualTo((ushort)bytes.Length));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(3, 4)), Is.EqualTo(0x00000001u));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(7, 4)), Is.EqualTo(0x000001CDu));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(11, 4)), Is.EqualTo(120u));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(15, 4)), Is.EqualTo(80u));
            }
        );
    }

    [Test]
    public void TryParse_ShouldFail_WhenDeclaredPacketLengthDoesNotMatchBufferLength()
    {
        var packet = new GenericGumpPacket
        {
            SenderSerial = 0x00000005,
            GumpId = 0x00000066,
            X = 10,
            Y = 20,
            Layout = "{ page 0 }"
        };

        var bytes = Write(packet);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(1, 2), (ushort)(bytes.Length + 1));

        var parsed = new GenericGumpPacket();
        var ok = parsed.TryParse(bytes);

        Assert.That(ok, Is.False);
    }

    [Test]
    public void TryParse_ShouldFail_WhenCommandSectionLengthIsInvalid()
    {
        var packet = new GenericGumpPacket
        {
            SenderSerial = 0x00000006,
            GumpId = 0x00000067,
            X = 1,
            Y = 2,
            Layout = "{ page 0 }"
        };

        var bytes = Write(packet);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(19, 2), ushort.MaxValue);

        var parsed = new GenericGumpPacket();
        var ok = parsed.TryParse(bytes);

        Assert.That(ok, Is.False);
    }

    private static byte[] Write(GenericGumpPacket packet)
    {
        var writer = new SpanWriter(1024, true);
        packet.Write(ref writer);
        var bytes = writer.ToArray();
        writer.Dispose();

        return bytes;
    }
}
