using Moongate.Core.Server.Packets;
using Moongate.Tests.Utilities;

namespace Moongate.Tests.Fixtures;

/// <summary>
/// Base fixture for testing READ-ONLY packets (Client→Server)
/// Provides standard test patterns for deserialization
/// </summary>
public abstract class ReadOnlyPacketTestFixture<TPacket>
    where TPacket : BaseUoPacket, new()
{
    /// <summary>
    /// Expected OpCode for this packet type (must be set by derived class)
    /// </summary>
    protected abstract byte ExpectedOpCode { get; }

    /// <summary>
    /// Creates a fresh packet instance for testing
    /// </summary>
    protected virtual TPacket CreatePacketInstance() => new();

    /// <summary>
    /// Creates valid serialized packet data for deserialization testing
    /// Derived classes must implement this to provide proper test data
    /// </summary>
    protected abstract ReadOnlyMemory<byte> CreateValidSerializedData();

    /// <summary>
    /// Standard test: OpCode validation
    /// </summary>
    [Test]
    public virtual void Packet_WithValidConstruction_ShouldHaveCorrectOpCode()
    {
        // Arrange
        var packet = CreatePacketInstance();

        // Act & Assert
        Assert.That(packet.OpCode, Is.EqualTo(ExpectedOpCode),
            $"OpCode should be 0x{ExpectedOpCode:X2}");
    }

    /// <summary>
    /// Standard test: Deserialization with valid data
    /// </summary>
    [Test]
    public virtual void Packet_ShouldDeserializeValidData()
    {
        // Arrange
        var packet = CreatePacketInstance();
        var validData = CreateValidSerializedData();

        // Act
        var result = packet.Read(validData);

        // Assert
        Assert.That(result, Is.True, "Deserialization should succeed with valid data");
    }

    /// <summary>
    /// Standard test: Deserialization fails with empty data
    /// </summary>
    [Test]
    public virtual void Packet_ShouldFailDeserializeEmptyData()
    {
        // Arrange
        var packet = CreatePacketInstance();

        // Act & Assert
        PacketTestHelpers.AssertDeserializeEmptyDataFails(packet);
    }

    /// <summary>
    /// Standard test: Deserialization fails with wrong OpCode
    /// </summary>
    [Test]
    public virtual void Packet_ShouldFailDeserializeWithWrongOpCode()
    {
        // Arrange
        var packet = CreatePacketInstance();

        // Act & Assert
        PacketTestHelpers.AssertDeserializeWrongOpCodeFails(packet, ExpectedOpCode);
    }
}
