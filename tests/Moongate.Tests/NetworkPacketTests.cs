using Moongate.Core.Spans;

namespace Moongate.Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void TestParsePacket()
    {
        var buffer = new byte[] { 0x01, 0x01, 0x02, 0x03, 0x04, 0x05 };
        var packet = new TestSimplePacket();
        var readResult = packet.Read(new ReadOnlyMemory<byte>(buffer));
        Assert.That(readResult, Is.True, "Packet should be read successfully");
    }

    [Test]
    public void TestWritePacket()
    {
        var packet = new TestSimplePacket { Number = 12345 };
        var writtenData = packet.Write(new SpanWriter(6));
        var expectedData = new byte[] { 0x01, 0x00, 0x00, 0x30, 0x39 }; // OpCode + Number in little-endian
        Assert.That(writtenData.ToArray(), Is.EqualTo(expectedData), "Written data should match expected data");
    }
}
