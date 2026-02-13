using Moongate.Core.Spans;
using Moongate.Tests.Utilities;
using Moongate.UO.Data.Packets.Login;

namespace Moongate.Tests.Packets.Login;

/// <summary>
/// TDD Tests for LoginRequestPacket (0x80)
/// Client→Server: Player sends login credentials
/// Based on Polserver protocol: https://docs.polserver.com/packets/index.php?packet=0x80
/// </summary>
public class LoginRequestPacketTests
{
    #region OpCode Validation Tests

    [Test]
    public void LoginRequestPacket_WithValidConstruction_ShouldHaveOpCode0x80()
    {
        // Arrange
        var packet = new LoginRequestPacket();

        // Act & Assert
        Assert.That(packet.OpCode, Is.EqualTo(0x80), "OpCode should be 0x80");
    }

    #endregion

    #region Deserialization Tests

    [Test]
    public void LoginRequestPacket_ShouldDeserializeValidCredentials()
    {
        // Arrange
        var packet = new LoginRequestPacket();
        const string accountName = "testaccount";
        const string password = "testpass";

        using var writer = new SpanWriter(62);
        writer.Write((byte)0x80);
        writer.WriteAscii(accountName, 30);
        writer.WriteAscii(password, 30);

        var serialized = writer.ToArray().AsMemory();

        // Act
        var result = packet.Read(serialized);

        // Assert
        Assert.That(result, Is.True, "Deserialization should succeed");
        Assert.That(packet.Account, Is.EqualTo(accountName));
        Assert.That(packet.Password, Is.EqualTo(password));
    }

    [Test]
    public void LoginRequestPacket_ShouldDeserializeEmptyAccountName()
    {
        // Arrange
        var packet = new LoginRequestPacket();

        using var writer = new SpanWriter(62);
        writer.Write((byte)0x80);
        writer.WriteAscii("", 30);
        writer.WriteAscii("password", 30);

        var serialized = writer.ToArray().AsMemory();

        // Act
        var result = packet.Read(serialized);

        // Assert
        Assert.That(result, Is.True, "Should handle empty account name");
        Assert.That(packet.Account, Is.Empty);
    }

    [Test]
    public void LoginRequestPacket_ShouldHandleMaxLengthStrings()
    {
        // Arrange
        var packet = new LoginRequestPacket();
        var accountName = new string('A', 29);
        var password = new string('B', 29);

        using var writer = new SpanWriter(62);
        writer.Write((byte)0x80);
        writer.WriteAscii(accountName, 30);
        writer.WriteAscii(password, 30);

        var serialized = writer.ToArray().AsMemory();

        // Act
        var result = packet.Read(serialized);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(packet.Account, Is.EqualTo(accountName));
        Assert.That(packet.Password, Is.EqualTo(password));
    }

    [Test]
    public void LoginRequestPacket_ShouldFailWithEmptyData()
    {
        // Arrange
        var packet = new LoginRequestPacket();

        // Act
        var result = packet.Read(ReadOnlyMemory<byte>.Empty);

        // Assert
        Assert.That(result, Is.False, "Should fail with empty data");
    }

    [Test]
    public void LoginRequestPacket_ShouldFailWithWrongOpCode()
    {
        // Arrange
        var packet = new LoginRequestPacket();

        using var writer = new SpanWriter(62);
        writer.Write((byte)0xFF); // Wrong opcode
        writer.WriteAscii("account", 30);
        writer.WriteAscii("password", 30);

        var serialized = writer.ToArray().AsMemory();

        // Act
        var result = packet.Read(serialized);

        // Assert
        Assert.That(result, Is.False, "Should fail with wrong OpCode");
    }

    [Test]
    public void LoginRequestPacket_ShouldFailWithTruncatedData()
    {
        // Arrange
        var packet = new LoginRequestPacket();

        using var writer = new SpanWriter(30);
        writer.Write((byte)0x80);
        writer.WriteAscii("account", 15);

        var serialized = writer.ToArray().AsMemory();

        // Act
        var result = packet.Read(serialized);

        // Assert
        Assert.That(result, Is.False, "Should fail with truncated data");
    }

    #endregion

    #region Property Tests

    [Test]
    public void LoginRequestPacket_ShouldStoreAccountNameProperty()
    {
        // Arrange
        var packet = new LoginRequestPacket();
        const string expected = "myaccount";

        // Act
        packet.Account = expected;

        // Assert
        Assert.That(packet.Account, Is.EqualTo(expected));
    }

    [Test]
    public void LoginRequestPacket_ShouldStorePasswordProperty()
    {
        // Arrange
        var packet = new LoginRequestPacket();
        const string expected = "mypassword";

        // Act
        packet.Password = expected;

        // Assert
        Assert.That(packet.Password, Is.EqualTo(expected));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void LoginRequestPacket_ShouldHandleBothEmptyCredentials()
    {
        // Arrange
        var packet = new LoginRequestPacket();

        using var writer = new SpanWriter(62);
        writer.Write((byte)0x80);
        writer.WriteAscii("", 30);
        writer.WriteAscii("", 30);

        var serialized = writer.ToArray().AsMemory();

        // Act
        var result = packet.Read(serialized);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(packet.Account, Is.Empty);
        Assert.That(packet.Password, Is.Empty);
    }


    #endregion
}
