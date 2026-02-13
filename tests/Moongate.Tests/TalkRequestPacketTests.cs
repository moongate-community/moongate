using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Packets.Characters;

namespace Moongate.Tests;

/// <summary>
/// TDD Tests for Talk Request Packet (0x03)
/// Client->Server: Player initiates NPC conversation
/// </summary>
public class TalkRequestPacketTests
{
    #region Packet Structure Tests

    [Test]
    public void TalkRequestPacket_WithValidConstruction_ShouldHaveCorrectOpCode()
    {
        // Arrange
        var packet = new TalkRequestPacket();

        // Act & Assert
        Assert.That(packet.OpCode, Is.EqualTo(0x03), "OpCode should be 0x03");
    }

    [Test]
    public void TalkRequestPacket_ShouldStoreNpcSerial()
    {
        // Arrange
        var packet = new TalkRequestPacket();
        var npcSerial = (Serial)0x12345678;

        // Act
        packet.NpcSerial = npcSerial;

        // Assert
        Assert.That(packet.NpcSerial, Is.EqualTo(npcSerial), "NpcSerial should be stored");
    }

    [Test]
    public void TalkRequestPacket_ShouldDeserializeNpcSerial()
    {
        // Arrange
        var packet = new TalkRequestPacket();
        var npcSerial = (Serial)0x87654321;
        using var writer = new SpanWriter(10);
        writer.Write((byte)0x03); // OpCode must be first
        writer.Write((uint)npcSerial.Value);
        var serialized = writer.ToArray().AsMemory();

        // Act
        var result = packet.Read(serialized);

        // Assert
        Assert.That(result, Is.True, "Read should succeed with valid serial");
        Assert.That(packet.NpcSerial, Is.EqualTo(npcSerial), "NpcSerial should be deserialized");
    }

    [Test]
    public void TalkRequestPacket_WithZeroSerial_ShouldFailRead()
    {
        // Arrange
        var packet = new TalkRequestPacket();
        using var writer = new SpanWriter(10);
        writer.Write((byte)0x03); // OpCode must be first
        writer.Write((uint)0x00000000); // Invalid serial (zero)
        var serialized = writer.ToArray().AsMemory();

        // Act
        var result = packet.Read(serialized);

        // Assert
        Assert.That(result, Is.False, "Read should fail with invalid (zero) serial");
    }

    #endregion

    #region Handler Tests

    [Test]
    public void TalkRequestHandler_ShouldBeRegisterable()
    {
        // Arrange & Act & Assert
        var handler = new Moongate.UO.PacketHandlers.TalkRequestHandler();

        // Just verify we can instantiate it (handler interface compliance)
        Assert.That(handler, Is.Not.Null, "Handler should be instantiable");
    }

    #endregion
}
