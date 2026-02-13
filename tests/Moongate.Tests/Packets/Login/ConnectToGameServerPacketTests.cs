using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.Login;

namespace Moongate.Tests.Packets.Login;

public class ConnectToGameServerPacketTests
{
    [Test]
    public void ConnectToGameServerPacket_ShouldHaveOpCode0x8C()
    {
        var packet = new ConnectToGameServerPacket();
        Assert.That(packet.OpCode, Is.EqualTo(0x8C));
    }

    [Test]
    public void ConnectToGameServerPacket_ShouldDeserializeValidData()
    {
        var packet = new ConnectToGameServerPacket();
        using var writer = new SpanWriter(11);
        writer.Write((byte)0x8C);
        writer.Write((uint)0x12345678); // AuthKey
        writer.Write((ushort)0x1234); // Port placeholder
        writer.Write((byte)0x01);

        var result = packet.Read(writer.ToArray().AsMemory());
        Assert.That(result, Is.True);
    }

    [Test]
    public void ConnectToGameServerPacket_ShouldFailWithEmptyData()
    {
        var packet = new ConnectToGameServerPacket();
        var result = packet.Read(ReadOnlyMemory<byte>.Empty);
        Assert.That(result, Is.False);
    }

    [Test]
    public void ConnectToGameServerPacket_ShouldFailWithWrongOpCode()
    {
        var packet = new ConnectToGameServerPacket();
        using var writer = new SpanWriter(11);
        writer.Write((byte)0xFF);
        writer.Write((uint)0);
        writer.Write((ushort)0);
        writer.Write((byte)0);

        var result = packet.Read(writer.ToArray().AsMemory());
        Assert.That(result, Is.False);
    }

    [Test]
    public void ConnectToGameServerPacket_ShouldHaveAuthKeyProperty()
    {
        var packet = new ConnectToGameServerPacket();
        packet.AuthKey = 0xDEADBEEFu;
        Assert.That(packet.AuthKey, Is.EqualTo(0xDEADBEEFu));
    }

    [Test]
    public void ConnectToGameServerPacket_ShouldHaveServerAddressProperty()
    {
        using var writer = new SpanWriter(10);
        var packet = new ConnectToGameServerPacket();
        packet.ServerAddress = System.Net.IPAddress.Parse("127.0.0.1");
        Assert.That(packet.ServerAddress.ToString(), Is.EqualTo("127.0.0.1"));
    }

    [Test]
    public void ConnectToGameServerPacket_ShouldHaveServerPortProperty()
    {
        var packet = new ConnectToGameServerPacket();
        packet.ServerPort = 2593;
        Assert.That(packet.ServerPort, Is.EqualTo(2593));
    }
}
