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
    public void ConnectToGameServerPacket_ShouldSerializeSuccessfully()
    {
        var packet = new ConnectToGameServerPacket
        {
            ServerAddress = System.Net.IPAddress.Parse("127.0.0.1"),
            ServerPort = 2593,
            AuthKey = 0x12345678
        };
        using var writer = new SpanWriter(20);
        var result = packet.Write(writer);

        Assert.That(result.Length, Is.GreaterThan(0));
        Assert.That(result.Span[0], Is.EqualTo(0x8C));
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
