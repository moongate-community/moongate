using System.Net;
using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.Data;
using Moongate.UO.Data.Packets.Login;

namespace Moongate.Tests.Packets.Login;

public class ShardListPacketTests
{
    [Test]
    public void ShardListPacket_ShouldHaveOpCode0xA8()
    {
        var entry = new GameServerEntry { Index = 1, ServerName = "Test Shard", IpAddress = IPAddress.Parse("127.0.0.1") };
        var packet = new ShardListPacket(entry);
        Assert.That(packet.OpCode, Is.EqualTo(0xA8));
    }

    [Test]
    public void ShardListPacket_ShouldSerializeSingleShard()
    {
        var entry = new GameServerEntry { Index = 1, ServerName = "Britannia", IpAddress = IPAddress.Parse("127.0.0.1") };
        var packet = new ShardListPacket(entry);

        using var writer = new SpanWriter(100);
        var result = packet.Write(writer);

        Assert.That(result.Length, Is.GreaterThan(0));
        Assert.That(result.Span[0], Is.EqualTo(0xA8));
    }

    [Test]
    public void ShardListPacket_ShouldSerializeMultipleShards()
    {
        var entry1 = new GameServerEntry { Index = 1, ServerName = "Britannia", IpAddress = IPAddress.Parse("127.0.0.1") };
        var entry2 = new GameServerEntry { Index = 2, ServerName = "Trammel", IpAddress = IPAddress.Parse("127.0.0.2") };
        var entry3 = new GameServerEntry { Index = 3, ServerName = "Felucca", IpAddress = IPAddress.Parse("127.0.0.3") };
        var packet = new ShardListPacket(entry1, entry2, entry3);

        using var writer = new SpanWriter(200);
        var result = packet.Write(writer);

        Assert.That(result.Length, Is.GreaterThan(0));
        Assert.That(result.Span[0], Is.EqualTo(0xA8));
    }

    [Test]
    public void ShardListPacket_ShouldHaveShardListProperty()
    {
        var entry = new GameServerEntry { Index = 1, ServerName = "Test", IpAddress = IPAddress.Parse("127.0.0.1") };
        var packet = new ShardListPacket(entry);
        Assert.That(packet.Shards, Is.Not.Null);
        Assert.That(packet.Shards.Count, Is.GreaterThan(0));
    }

    [Test]
    public void ShardListPacket_ShouldAddShardViaAddShard()
    {
        var packet = new ShardListPacket(new GameServerEntry { Index = 1, ServerName = "Test", IpAddress = IPAddress.Parse("127.0.0.1") });
        var newEntry = new GameServerEntry { Index = 2, ServerName = "New Shard", IpAddress = IPAddress.Parse("127.0.0.2") };
        packet.AddShard(newEntry);

        Assert.That(packet.Shards.Count, Is.EqualTo(2));
    }

    [Test]
    public void ShardListPacket_ShouldContainCorrectShardIndex()
    {
        var entry = new GameServerEntry { Index = 42, ServerName = "Custom Shard", IpAddress = IPAddress.Parse("127.0.0.1") };
        var packet = new ShardListPacket(entry);

        Assert.That(packet.Shards[0].Index, Is.EqualTo(42));
    }

    [Test]
    public void ShardListPacket_ShouldSerializeWithMaxShards()
    {
        var packet = new ShardListPacket();
        for (int i = 0; i < 255; i++)
        {
            packet.AddShard(new GameServerEntry { Index = i, ServerName = $"Shard {i}", IpAddress = IPAddress.Parse("127.0.0.1") });
        }

        using var writer = new SpanWriter(15000);
        var result = packet.Write(writer);

        Assert.That(result.Length, Is.GreaterThan(0));
        Assert.That(result.Span[0], Is.EqualTo(0xA8));
    }

    [Test]
    public void ShardListPacket_ShouldPreserveShardName()
    {
        var testName = "Britannia";
        var entry = new GameServerEntry { Index = 1, ServerName = testName, IpAddress = IPAddress.Parse("127.0.0.1") };
        var packet = new ShardListPacket(entry);

        Assert.That(packet.Shards[0].ServerName, Is.EqualTo(testName));
    }
}
