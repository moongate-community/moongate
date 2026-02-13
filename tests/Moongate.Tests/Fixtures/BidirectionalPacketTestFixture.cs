using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.Tests.Fixtures;

/// <summary>
/// Base fixture for testing BIDIRECTIONAL packets (Client←→Server)
/// Provides standard test patterns for both serialization and deserialization
/// </summary>
public abstract class BidirectionalPacketTestFixture<TPacket>
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
    /// Configures packet with test data before serialization
    /// Derived classes should override this to set packet properties
    /// </summary>
    protected virtual void ConfigurePacket(TPacket packet) { }

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
    /// Standard test: Serialization (Write direction)
    /// </summary>
    [Test]
    public virtual void Packet_ShouldSerializeWithCorrectOpCode()
    {
        // Arrange
        var packet = CreatePacketInstance();
        ConfigurePacket(packet);

        using var writer = new SpanWriter(100);

        // Act
        var serialized = packet.Write(writer);

        // Assert
        Assert.That(serialized.Length, Is.GreaterThan(0), "Serialized packet should not be empty");
        Assert.That(serialized.Span[0], Is.EqualTo(ExpectedOpCode),
            $"OpCode should be 0x{ExpectedOpCode:X2}");
    }

    /// <summary>
    /// Standard test: Deserialization (Read direction)
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
    /// Standard test: Round-trip serialization and deserialization
    /// </summary>
    [Test]
    public virtual void Packet_ShouldRoundTripSerializationDeserialization()
    {
        // Arrange
        var original = CreatePacketInstance();
        ConfigurePacket(original);

        using var writer = new SpanWriter(100);

        // Act - Serialize
        var serialized = original.Write(writer);

        // Act - Deserialize
        var deserialized = CreatePacketInstance();
        var result = deserialized.Read(serialized);

        // Assert
        Assert.That(result, Is.True, "Deserialization should succeed after serialization");
        Assert.That(deserialized.OpCode, Is.EqualTo(original.OpCode),
            "OpCode should match after round trip");
    }
}
