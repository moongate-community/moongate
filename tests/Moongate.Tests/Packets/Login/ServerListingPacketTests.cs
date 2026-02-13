using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.Data;
using Moongate.UO.Data.Packets.Login;

namespace Moongate.Tests.Packets.Login;

public class ServerListingPacketTests
{
    [Test]
    public void ServerListingPacket_ShouldHaveOpCode0x5E()
    {
        var packet = new ServerListingPacket();
        Assert.That(packet.OpCode, Is.EqualTo(0x5E));
    }

    [Test]
    public void ServerListingPacket_ShouldSerializeEmptyList()
    {
        var packet = new ServerListingPacket();
        using var writer = new SpanWriter(50);
        var result = packet.Write(writer);

        Assert.That(result.Length, Is.GreaterThan(0));
        Assert.That(result.Span[0], Is.EqualTo(0x5E));
    }

    [Test]
    public void ServerListingPacket_ShouldSerializeSingleServer()
    {
        var packet = new ServerListingPacket();
        var entry = new ServerEntry { Index = 1, ServerName = "Test Server" };
        packet.AddServer(entry);

        using var writer = new SpanWriter(100);
        var result = packet.Write(writer);

        Assert.That(result.Length, Is.GreaterThan(0));
        Assert.That(result.Span[0], Is.EqualTo(0x5E));
    }

    [Test]
    public void ServerListingPacket_ShouldSerializeMultipleServers()
    {
        var packet = new ServerListingPacket();
        packet.AddServer(new ServerEntry { Index = 1, ServerName = "Server 1" });
        packet.AddServer(new ServerEntry { Index = 2, ServerName = "Server 2" });
        packet.AddServer(new ServerEntry { Index = 3, ServerName = "Server 3" });

        using var writer = new SpanWriter(200);
        var result = packet.Write(writer);

        Assert.That(result.Length, Is.GreaterThan(0));
        Assert.That(result.Span[0], Is.EqualTo(0x5E));
    }

    [Test]
    public void ServerListingPacket_ShouldHaveServerListProperty()
    {
        var packet = new ServerListingPacket();
        Assert.That(packet.Servers, Is.Not.Null);
        Assert.That(packet.Servers.Count, Is.EqualTo(0));
    }

    [Test]
    public void ServerListingPacket_ShouldAddServerViaConstructor()
    {
        var entry1 = new ServerEntry { Index = 1, ServerName = "Server 1" };
        var entry2 = new ServerEntry { Index = 2, ServerName = "Server 2" };
        var packet = new ServerListingPacket(entry1, entry2);

        Assert.That(packet.Servers.Count, Is.EqualTo(2));
    }

    [Test]
    public void ServerListingPacket_ShouldSerializeWithMaxServers()
    {
        var packet = new ServerListingPacket();
        for (int i = 0; i < 255; i++)
        {
            packet.AddServer(new ServerEntry { Index = (byte)i, ServerName = $"Server {i}" });
        }

        using var writer = new SpanWriter(10000);
        var result = packet.Write(writer);

        Assert.That(result.Length, Is.GreaterThan(0));
        Assert.That(result.Span[0], Is.EqualTo(0x5E));
    }

    [Test]
    public void ServerListingPacket_ShouldPreserveServerNames()
    {
        var packet = new ServerListingPacket();
        var testName = "Britania";
        packet.AddServer(new ServerEntry { Index = 1, ServerName = testName });

        using var writer = new SpanWriter(100);
        packet.Write(writer);

        // Verify the server was added with correct properties
        Assert.That(packet.Servers[0].ServerName, Is.EqualTo(testName));
    }
}
