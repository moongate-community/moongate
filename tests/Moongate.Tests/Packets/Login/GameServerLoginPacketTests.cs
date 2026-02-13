using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.Login;

namespace Moongate.Tests.Packets.Login;

/// <summary>
/// TDD Tests for GameServerLoginPacket (0x91)
/// Client→Server: Player connects to game server with auth
/// Based on Polserver protocol
/// </summary>
public class GameServerLoginPacketTests
{
    #region OpCode Tests

    [Test]
    public void GameServerLoginPacket_ShouldHaveOpCode0x91()
    {
        var packet = new GameServerLoginPacket();
        Assert.That(packet.OpCode, Is.EqualTo(0x91));
    }

    #endregion

    #region Deserialization Tests

    [Test]
    public void GameServerLoginPacket_ShouldDeserializeValidData()
    {
        var packet = new GameServerLoginPacket();
        using var writer = new SpanWriter(65);
        writer.Write((byte)0x91);
        writer.Write((uint)0x12345678); // AuthKey
        writer.WriteAscii("testaccount", 30);
        writer.WriteAscii("testpass", 30);

        var result = packet.Read(writer.ToArray().AsMemory());
        Assert.That(result, Is.True);
    }

    [Test]
    public void GameServerLoginPacket_ShouldFailWithEmptyData()
    {
        var packet = new GameServerLoginPacket();
        var result = packet.Read(ReadOnlyMemory<byte>.Empty);
        Assert.That(result, Is.False);
    }

    [Test]
    public void GameServerLoginPacket_ShouldFailWithWrongOpCode()
    {
        var packet = new GameServerLoginPacket();
        using var writer = new SpanWriter(65);
        writer.Write((byte)0xFF);
        writer.Write((uint)0x12345678);
        writer.WriteAscii("test", 30);
        writer.WriteAscii("test", 30);

        var result = packet.Read(writer.ToArray().AsMemory());
        Assert.That(result, Is.False);
    }

    [Test]
    public void GameServerLoginPacket_ShouldHandleEmptyCredentials()
    {
        var packet = new GameServerLoginPacket();
        using var writer = new SpanWriter(65);
        writer.Write((byte)0x91);
        writer.Write((uint)0);
        writer.WriteAscii("", 30);
        writer.WriteAscii("", 30);

        var result = packet.Read(writer.ToArray().AsMemory());
        Assert.That(result, Is.True);
    }

    [Test]
    public void GameServerLoginPacket_ShouldHandleMaxLengthStrings()
    {
        var packet = new GameServerLoginPacket();
        var account = new string('A', 29);
        var password = new string('B', 29);

        using var writer = new SpanWriter(65);
        writer.Write((byte)0x91);
        writer.Write((uint)0xFFFFFFFF);
        writer.WriteAscii(account, 30);
        writer.WriteAscii(password, 30);

        var result = packet.Read(writer.ToArray().AsMemory());
        Assert.That(result, Is.True);
    }

    [Test]
    public void GameServerLoginPacket_ShouldStoreAuthKey()
    {
        var packet = new GameServerLoginPacket();
        packet.AuthKey = 0x87654321;
        Assert.That(packet.AuthKey, Is.EqualTo(0x87654321u));
    }

    [Test]
    public void GameServerLoginPacket_ShouldStoreAccountName()
    {
        var packet = new GameServerLoginPacket();
        packet.AccountName = "myaccount";
        Assert.That(packet.AccountName, Is.EqualTo("myaccount"));
    }

    [Test]
    public void GameServerLoginPacket_ShouldStorePassword()
    {
        var packet = new GameServerLoginPacket();
        packet.Password = "mypass";
        Assert.That(packet.Password, Is.EqualTo("mypass"));
    }

    #endregion
}
