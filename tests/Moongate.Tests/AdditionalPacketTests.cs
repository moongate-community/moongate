using Moongate.Core.Spans;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Packets.GeneralInformation;
using Moongate.UO.Data.Packets.GeneralInformation.Factory;
using Moongate.UO.Data.Packets.GeneralInformation.SubCommands;
using Moongate.UO.Data.Packets.GeneralInformation.Types;
using Moongate.UO.Data.Packets.Skills;
using Moongate.UO.Data.Packets.World;
using Moongate.UO.Data.Types;

namespace Moongate.Tests;

/// <summary>
/// TDD Tests for additional packets: SkillUpdatePacket, SetMusicPacket, GeneralInformationFactory, Subcommand data
/// </summary>
public class AdditionalPacketTests
{
    #region SkillUpdatePacket Tests

    [Test]
    public void SkillUpdatePacket_WithSkillList_ShouldHaveCorrectOpCode()
    {
        // Arrange
        var packet = new SkillUpdatePacket();

        // Act & Assert
        Assert.That(packet.OpCode, Is.EqualTo(0x3A), "OpCode should be 0x3A");
    }

    [Test]
    public void SkillUpdatePacket_DefaultType_ShouldBeSkillRequest()
    {
        // Arrange
        var packet = new SkillUpdatePacket();

        // Assert
        Assert.That(packet.UpdateType, Is.EqualTo(SkillUpdatePacket.SkillUpdateType.SkillRequest),
            "Default type should be SkillRequest (for client→server communication)");
    }

    [Test]
    public void SkillUpdatePacket_ShouldSerializeSuccessfully()
    {
        // Arrange
        var packet = new SkillUpdatePacket();

        // Act
        var writer = new SpanWriter(100);
        var serialized = packet.Write(writer);

        // Assert
        Assert.That(serialized.Length, Is.GreaterThan(0), "Packet should serialize");
        Assert.That(serialized.Span[0], Is.EqualTo(0x3A), "OpCode should be 0x3A");
    }

    #endregion

    #region SetMusicPacket Tests

    [Test]
    public void SetMusicPacket_WithIntConstructor_ShouldStoreMusic()
    {
        // Arrange
        var musicId = 42;

        // Act
        var packet = new SetMusicPacket(musicId);

        // Assert
        Assert.That(packet.MusicId, Is.EqualTo(42), "MusicId should be stored");
        Assert.That(packet.OpCode, Is.EqualTo(0x6D), "OpCode should be 0x6D");
    }

    [Test]
    public void SetMusicPacket_WithMusicNameEnum_ShouldConvertToInt()
    {
        // Arrange
        var musicName = MusicName.Stones2;

        // Act
        var packet = new SetMusicPacket(musicName);

        // Assert
        Assert.That(packet.MusicId, Is.EqualTo((int)musicName), "Should convert enum to int");
    }

    [Test]
    public void SetMusicPacket_ShouldSerializeWithCorrectFormat()
    {
        // Arrange
        var packet = new SetMusicPacket(100);

        // Act
        var writer = new SpanWriter(50);
        var serialized = packet.Write(writer);

        // Assert
        Assert.That(serialized.Length, Is.EqualTo(3), "Packet should be 3 bytes: opcode(1) + musicId(2)");
        Assert.That(serialized.Span[0], Is.EqualTo(0x6D), "Byte 0 should be OpCode");
    }

    #endregion

    #region GeneralInformationFactory Tests

    [Test]
    public void GeneralInformationFactory_CreateSetCursorHueSetMap_ShouldReturnValidPacket()
    {
        // Arrange & Act
        var packet = GeneralInformationFactory.CreateSetCursorHueSetMap(Map.Felucca);

        // Assert
        Assert.That(packet, Is.Not.Null, "Packet should be created");
        Assert.That(packet.OpCode, Is.EqualTo(0xBF), "OpCode should be 0xBF");
        Assert.That(packet.SubcommandType, Is.EqualTo(SubcommandType.SetCursorHueSetMap), "Subcommand should be SetCursorHueSetMap");
    }

    [Test]
    public void GeneralInformationFactory_CreateInitializeFastWalkPrevention_ShouldRequireSixKeys()
    {
        // Arrange
        var validKeys = new uint[] { 1, 2, 3, 4, 5, 6 };

        // Act
        var packet = GeneralInformationFactory.CreateInitializeFastWalkPrevention(validKeys);

        // Assert
        Assert.That(packet, Is.Not.Null, "Packet should be created");
        Assert.That(packet.SubcommandType, Is.EqualTo(SubcommandType.InitializeFastWalkPrevention),
            "Subcommand should be InitializeFastWalkPrevention");
    }

    [Test]
    public void GeneralInformationFactory_CreateMountSpeed_ShouldCreateValidPacket()
    {
        // Arrange & Act
        var packet = GeneralInformationFactory.CreateMountSpeed(1);

        // Assert
        Assert.That(packet, Is.Not.Null, "Packet should be created");
        Assert.That(packet.SubcommandType, Is.EqualTo(SubcommandType.MountSpeed), "Subcommand should be MountSpeed");
    }

    #endregion

    #region Subcommand Data Tests

    [Test]
    public void SetCursorHueSetMapData_ShouldSerializeMapId()
    {
        // Arrange
        var data = new SetCursorHueSetMapData();
        data.MapId = 2;  // Ilshenar MapID

        // Act
        var writer = new SpanWriter(10);
        var serialized = data.Write(writer);

        // Assert
        Assert.That(serialized.Length, Is.EqualTo(1), "Data should be 1 byte");
        Assert.That(serialized.Span[0], Is.EqualTo(2), "MapID should be serialized as 2");
    }

    [Test]
    public void ScreenSizeData_ShouldHaveCorrectLength()
    {
        // Arrange
        var data = new ScreenSizeData();

        // Act & Assert
        Assert.That(data.Length, Is.EqualTo(8), "ScreenSizeData should be 8 bytes");
    }

    [Test]
    public void ScreenSizeData_ShouldSerializeCorrectly()
    {
        // Arrange
        var data = new ScreenSizeData { Width = 1024, Height = 768 };

        // Act
        var writer = new SpanWriter(20);
        var serialized = data.Write(writer);

        // Assert
        Assert.That(serialized.Length, Is.EqualTo(8), "Serialized data should be 8 bytes");
    }

    [Test]
    public void ClientLanguageData_ShouldBeParseable()
    {
        // Arrange
        var data = new ClientLanguageData();

        // Act & Assert
        Assert.That(data, Is.Not.Null, "ClientLanguageData should be instantiable");
    }

    #endregion

    #region GeneralInformationPacket Integration Tests

    [Test]
    public void GeneralInformationPacket_WithSubcommandData_ShouldSerialize()
    {
        // Arrange
        var mapData = new SetCursorHueSetMapData(Map.Trammel);
        var packet = new GeneralInformationPacket(SubcommandType.SetCursorHueSetMap, mapData);

        // Act
        var writer = new SpanWriter(50);
        var serialized = packet.Write(writer);

        // Assert
        Assert.That(serialized.Length, Is.GreaterThan(0), "Packet should serialize");
        Assert.That(serialized.Span[0], Is.EqualTo(0xBF), "OpCode should be 0xBF");
    }

    [Test]
    public void GeneralInformationPacket_Length_ShouldIncludeAllComponents()
    {
        // Arrange
        var mapData = new SetCursorHueSetMapData(Map.Felucca);
        var packet = new GeneralInformationPacket(SubcommandType.SetCursorHueSetMap, mapData);

        // Act
        var writer = new SpanWriter(50);
        var serialized = packet.Write(writer);

        // Assert: Length = opcode(1) + length(2) + subcommand(2) + data(1) = 6 bytes
        Assert.That(serialized.Length, Is.EqualTo(6), "Packet should be 6 bytes");
    }

    #endregion

    #region Edge Cases

    [Test]
    public void SetMusicPacket_WithZeroMusic_ShouldStoreZero()
    {
        // Arrange
        var packet = new SetMusicPacket(0);

        // Act & Assert
        Assert.That(packet.MusicId, Is.EqualTo(0), "MusicId should support zero");
    }

    [Test]
    public void SetMusicPacket_WithMaxUshort_ShouldStoreMaxValue()
    {
        // Arrange
        var packet = new SetMusicPacket(65535);

        // Act & Assert
        Assert.That(packet.MusicId, Is.EqualTo(65535), "MusicId should support max ushort");
    }

    [Test]
    public void GeneralInformationPacket_CreateWithRawData_ShouldWork()
    {
        // Arrange
        var rawData = new byte[] { 0x01, 0x02 }.AsMemory();

        // Act
        var packet = new GeneralInformationPacket(SubcommandType.SetCursorHueSetMap, rawData);

        // Assert
        Assert.That(packet.SubcommandType, Is.EqualTo(SubcommandType.SetCursorHueSetMap), "Subcommand should be set");
    }

    #endregion
}
