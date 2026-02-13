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
    public void LoginRequestPacket_ShouldAllowNullOrEmptyStrings()
    {
        var packet = new LoginRequestPacket();
        packet.Account = null;
        packet.Password = null;
        Assert.That(packet.Account, Is.Null);
        Assert.That(packet.Password, Is.Null);
    }

    #endregion
}
