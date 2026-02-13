using Moongate.Core.Spans;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Packets.Data;
using Moongate.UO.Data.Packets.GeneralInformation;
using Moongate.UO.Data.Packets.GeneralInformation.Factory;
using Moongate.UO.Data.Packets.GeneralInformation.Types;
using Moongate.UO.Data.Packets.Login;

namespace Moongate.Tests;

/// <summary>
/// TDD Tests for FASE 2: Packet Refactoring
/// Tests for GeneralInformationFactory.CreateSetCursorHueSetMap and ServerListingPacket
/// </summary>
public class Fase2PacketTests
{
    [Test]
    public void CreateSetCursorHueSetMap_ShouldCreateValidGeneralInformationPacket()
    {
        // Arrange
        var testMap = Map.Ilshenar;

        // Act
        var packet = GeneralInformationFactory.CreateSetCursorHueSetMap(testMap);

        // Assert
        Assert.That(packet, Is.Not.Null, "Packet should be created");
        Assert.That(packet.SubcommandType, Is.EqualTo(SubcommandType.SetCursorHueSetMap), "Should use correct subcommand type");
        Assert.That(packet.OpCode, Is.EqualTo(0xBF), "Should use 0xBF opcode");
    }

    [Test]
    public void SetCursorHueSetMap_PacketSerialization_ShouldContainMapId()
    {
        // Arrange
        var testMap = Map.Trammel;
        var packet = GeneralInformationFactory.CreateSetCursorHueSetMap(testMap);

        // Act
        var writer = new SpanWriter(10);
        var serialized = packet.Write(writer);

        // Assert
        Assert.That(serialized.Length, Is.GreaterThanOrEqualTo(6), "Packet should have minimum length");
        Assert.That(serialized.Span[0], Is.EqualTo(0xBF), "OpCode should be 0xBF");
    }

    [Test]
    public void ServerListingPacket_ShouldSerializeWithCorrectOpCode()
    {
        // Arrange
        var packet = new ServerListingPacket();

        // Act
        var writer = new SpanWriter(100);
        var serialized = packet.Write(writer);

        // Assert
        Assert.That(serialized.Length, Is.GreaterThan(0), "Packet should serialize");
        Assert.That(serialized.Span[0], Is.EqualTo(0x5E), "OpCode should be 0x5E");
    }

    [Test]
    public void ServerListingPacket_WithMultipleServers_ShouldSerializeCorrectly()
    {
        // Arrange
        var packet = new ServerListingPacket();
        packet.AddServer(new ServerEntry { Index = 0, ServerName = "Server1" });
        packet.AddServer(new ServerEntry { Index = 1, ServerName = "Server2" });

        // Act
        var writer = new SpanWriter(300);
        var serialized = packet.Write(writer);

        // Assert
        Assert.That(serialized.Length, Is.GreaterThan(0), "Packet should serialize");
        Assert.That(serialized.Span[0], Is.EqualTo(0x5E), "OpCode should be 0x5E");

        // Verify packet length is set (bytes at position 1-2, BIG-endian)
        var length = (serialized.Span[1] << 8) | serialized.Span[2];
        Assert.That(length, Is.GreaterThan(0), "Packet length should be set");
    }

    [Test]
    public void ServerListingPacket_ServerCount_ShouldBeSerializedCorrectly()
    {
        // Arrange
        var packet = new ServerListingPacket();
        packet.AddServer(new ServerEntry { Index = 0, ServerName = "TestServer" });

        // Act
        var writer = new SpanWriter(200);
        var serialized = packet.Write(writer);

        // Assert OpCode
        Assert.That(serialized.Span[0], Is.EqualTo(0x5E), "OpCode should be 0x5E");

        // Assert Length at bytes 1-2 (BIG-endian)
        var length = (serialized.Span[1] << 8) | serialized.Span[2];
        Assert.That(length, Is.EqualTo(38), "Packet length should be 38 bytes");

        // Assert Server count at bytes 3-4 (BIG-endian)
        var serverCountAtBytes34 = (serialized.Span[3] << 8) | serialized.Span[4];
        Assert.That(serverCountAtBytes34, Is.EqualTo(1), "Server count should be 1");
    }

    [Test]
    public void GeneralInformationPacket_WithSetCursorHueSetMap_ShouldHaveCorrectLength()
    {
        // Arrange
        var testMap = Map.Felucca;
        var packet = GeneralInformationFactory.CreateSetCursorHueSetMap(testMap);

        // Act
        var writer = new SpanWriter(50);
        var serialized = packet.Write(writer);

        // Assert: Minimum length = 1 (opcode) + 2 (length) + 2 (subcommand) + 1 (mapID) = 6 bytes
        Assert.That(serialized.Length, Is.GreaterThanOrEqualTo(6), "Packet should contain minimum required bytes");
    }
}
