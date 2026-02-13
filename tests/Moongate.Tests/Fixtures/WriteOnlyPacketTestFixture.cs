using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.Tests.Utilities;

namespace Moongate.Tests.Fixtures;

/// <summary>
/// Base fixture for testing WRITE-ONLY packets (Server→Client)
/// Provides standard test patterns for serialization
/// </summary>
public abstract class WriteOnlyPacketTestFixture<TPacket>
    where TPacket : BaseUoPacket, new()
{
    /// <summary>
    /// Expected OpCode for this packet type (must be set by derived class)
    /// </summary>
    protected abstract byte ExpectedOpCode { get; }

    /// <summary>
    /// Expected minimum packet length in bytes
    /// </summary>
    protected virtual int ExpectedMinimumLength => 1; // Just OpCode

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
    /// Standard test: Serialization produces data with correct OpCode
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
        PacketTestHelpers.AssertSerializationValid(serialized, ExpectedOpCode);
    }

    /// <summary>
    /// Standard test: Serialization produces non-empty data
    /// </summary>
    [Test]
    public virtual void Packet_ShouldSerializeToNonEmptyData()
    {
        // Arrange
        var packet = CreatePacketInstance();
        ConfigurePacket(packet);

        using var writer = new SpanWriter(100);

        // Act
        var serialized = packet.Write(writer);

        // Assert
        Assert.That(serialized.Length, Is.GreaterThanOrEqualTo(ExpectedMinimumLength),
            $"Serialized packet should be at least {ExpectedMinimumLength} bytes");
    }

    /// <summary>
    /// Standard test: Packet length field is correct (for variable-length packets)
    /// </summary>
    [Test]
    public virtual void Packet_ShouldSerializeWithValidLength()
    {
        // Arrange
        var packet = CreatePacketInstance();
        ConfigurePacket(packet);

        using var writer = new SpanWriter(100);

        // Act
        var serialized = packet.Write(writer);

        // Assert - Only validate if packet has length field (3+ bytes)
        if (serialized.Length >= 3)
        {
            PacketTestHelpers.AssertPacketLengthValid(serialized);
        }
    }
}
