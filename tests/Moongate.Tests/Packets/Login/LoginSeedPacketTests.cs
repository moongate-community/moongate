using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.Login;

namespace Moongate.Tests.Packets.Login;

public class LoginSeedPacketTests
{
    [Test]
    public void LoginSeedPacket_ShouldHaveOpCode0xEF()
    {
        var packet = new LoginSeedPacket();
        Assert.That(packet.OpCode, Is.EqualTo(0xEF));
    }

    [Test]
    public void LoginSeedPacket_ShouldDeserializeValidData()
    {
        var packet = new LoginSeedPacket();
        using var writer = new SpanWriter(21);
        writer.Write((byte)0xEF);
        writer.Write((uint)0x12345678); // Seed
        writer.Write(new byte[16]); // Version info

        var result = packet.Read(writer.ToArray().AsMemory());
        Assert.That(result, Is.True);
    }

    [Test]
    public void LoginSeedPacket_ShouldFailWithEmptyData()
    {
        var packet = new LoginSeedPacket();
        var result = packet.Read(ReadOnlyMemory<byte>.Empty);
        Assert.That(result, Is.False);
    }

    [Test]
    public void LoginSeedPacket_ShouldFailWithWrongOpCode()
    {
        var packet = new LoginSeedPacket();
        using var writer = new SpanWriter(21);
        writer.Write((byte)0xFF);
        writer.Write((uint)0);
        writer.Write(new byte[16]);

        var result = packet.Read(writer.ToArray().AsMemory());
        Assert.That(result, Is.False);
    }

    [Test]
    public void LoginSeedPacket_ShouldSerializeSuccessfully()
    {
        var packet = new LoginSeedPacket { Seed = 0x12345678 };
        using var writer = new SpanWriter(50);
        var result = packet.Write(writer);

        Assert.That(result.Length, Is.GreaterThan(0));
        Assert.That(result.Span[0], Is.EqualTo(0xEF));
    }

    [Test]
    public void LoginSeedPacket_ShouldHaveSeedProperty()
    {
        var packet = new LoginSeedPacket();
        packet.Seed = 0x12345678;
        Assert.That(packet.Seed, Is.EqualTo(0x12345678));
    }
}
