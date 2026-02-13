using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Spans;

namespace Moongate.Tests.Utilities;

/// <summary>
/// Reusable helpers for packet testing following TDD patterns
/// </summary>
public static class PacketTestHelpers
{
    /// <summary>
    /// Creates a SpanWriter with appropriate sizing for packet data
    /// </summary>
    public static SpanWriter CreateSpanWriter(int expectedSize = 100)
        => new(expectedSize);

    /// <summary>
    /// Creates test serialized packet data with OpCode and payload
    /// </summary>
    public static ReadOnlyMemory<byte> CreateTestSerializedPacket(byte opCode, params byte[] payload)
    {
        using var writer = new SpanWriter(1 + payload.Length);
        writer.Write(opCode);

        if (payload.Length > 0)
        {
            writer.Write(payload.AsSpan());
        }

        return writer.ToArray().AsMemory();
    }

    /// <summary>
    /// Creates test serialized packet with OpCode and uint value (big-endian for UO protocol)
    /// </summary>
    public static ReadOnlyMemory<byte> CreateTestSerializedPacketWithUint(byte opCode, uint value)
    {
        using var writer = new SpanWriter(5);
        writer.Write(opCode);
        writer.Write(value);
        return writer.ToArray().AsMemory();
    }

    /// <summary>
    /// Creates test serialized packet with OpCode and ushort value
    /// </summary>
    public static ReadOnlyMemory<byte> CreateTestSerializedPacketWithUshort(byte opCode, ushort value)
    {
        using var writer = new SpanWriter(3);
        writer.Write(opCode);
        writer.Write(value);
        return writer.ToArray().AsMemory();
    }

    /// <summary>
    /// Asserts that packet OpCode matches expected value
    /// </summary>
    public static void AssertOpCodeMatch(IUoNetworkPacket packet, byte expectedOpCode)
    {
        if (packet.OpCode != expectedOpCode)
        {
            throw new AssertionException(
                $"OpCode mismatch: Expected 0x{expectedOpCode:X2}, but got 0x{packet.OpCode:X2}"
            );
        }
    }

    /// <summary>
    /// Asserts packet serialization produces non-empty data with correct OpCode
    /// </summary>
    public static void AssertSerializationValid(ReadOnlyMemory<byte> serialized, byte expectedOpCode)
    {
        if (serialized.IsEmpty)
        {
            throw new AssertionException("Serialized packet data is empty");
        }

        if (serialized.Span[0] != expectedOpCode)
        {
            throw new AssertionException(
                $"First byte (OpCode) mismatch: Expected 0x{expectedOpCode:X2}, but got 0x{serialized.Span[0]:X2}"
            );
        }
    }

    /// <summary>
    /// Asserts that deserialization fails with empty data
    /// </summary>
    public static void AssertDeserializeEmptyDataFails(IUoNetworkPacket packet)
    {
        var result = packet.Read(ReadOnlyMemory<byte>.Empty);

        if (result)
        {
            throw new AssertionException("Read() should fail on empty data but succeeded");
        }
    }

    /// <summary>
    /// Asserts that deserialization fails with wrong OpCode
    /// </summary>
    public static void AssertDeserializeWrongOpCodeFails(IUoNetworkPacket packet, byte expectedOpCode)
    {
        using var writer = new SpanWriter(10);
        writer.Write((byte)(expectedOpCode ^ 0xFF)); // Write wrong opcode

        var result = packet.Read(writer.ToArray().AsMemory());

        if (result)
        {
            throw new AssertionException("Read() should fail with wrong OpCode but succeeded");
        }
    }

    /// <summary>
    /// Extracts packet length from serialized data (big-endian format at bytes 1-2)
    /// </summary>
    public static ushort ExtractPacketLength(ReadOnlySpan<byte> serializedData)
    {
        if (serializedData.Length < 3)
        {
            throw new AssertionException($"Packet too short ({serializedData.Length} bytes) to contain length field");
        }

        return (ushort)((serializedData[1] << 8) | serializedData[2]);
    }

    /// <summary>
    /// Asserts that packet length field matches actual serialized length
    /// </summary>
    public static void AssertPacketLengthValid(ReadOnlyMemory<byte> serialized)
    {
        if (serialized.Length < 3)
        {
            return; // Packet too short to have length field
        }

        var expectedLength = ExtractPacketLength(serialized.Span);

        if (expectedLength != serialized.Length)
        {
            throw new AssertionException(
                $"Packet length field mismatch: Field indicates {expectedLength} bytes, but actual data is {serialized.Length} bytes"
            );
        }
    }

    /// <summary>
    /// Compares two serialized packet data blocks
    /// </summary>
    public static void ComparePacketData(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual, string fieldName = "packet data")
    {
        if (!expected.SequenceEqual(actual))
        {
            var expectedHex = string.Join(" ", expected.ToArray().Select(b => $"{b:X2}"));
            var actualHex = string.Join(" ", actual.ToArray().Select(b => $"{b:X2}"));

            throw new AssertionException(
                $"{fieldName} mismatch:\n  Expected: {expectedHex}\n  Actual:   {actualHex}"
            );
        }
    }
}
