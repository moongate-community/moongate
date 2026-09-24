using System.Buffers.Binary;
using System.Text;
using Moongate.Core.Primitives;

namespace Moongate.Network.Packets.Spans;

public ref struct PacketReader
{
    private readonly ReadOnlySpan<byte> _source;

    public int Position { get; private set; }
    public readonly int Remaining => _source.Length - Position;

    public PacketReader(ReadOnlySpan<byte> source)
    {
        _source = source;
        Position = 0;
    }

    public bool TryReadAscii(int byteCount, out string? value)
    {
        value = null;

        if (!TryGetBytes(byteCount, out var bytes) || bytes.Contains((byte)0) || !IsAscii(bytes))
        {
            return false;
        }

        value = Encoding.ASCII.GetString(bytes);
        Position += byteCount;

        return true;
    }

    public bool TryReadByte(out byte value)
    {
        if (Remaining < 1)
        {
            value = default;

            return false;
        }

        value = _source[Position];
        Position++;

        return true;
    }

    public bool TryReadBytes(int length, out ReadOnlySpan<byte> value)
    {
        if (length < 0 || Remaining < length)
        {
            value = default;

            return false;
        }

        value = _source.Slice(Position, length);
        Position += length;

        return true;
    }

    public bool TryReadFixedAscii(int byteCount, out string? value)
    {
        value = null;

        if (!TryGetBytes(byteCount, out var bytes))
        {
            return false;
        }

        var terminator = bytes.IndexOf((byte)0);
        var textBytes = terminator < 0 ? bytes : bytes[..terminator];

        if (!IsAscii(textBytes))
        {
            return false;
        }

        value = Encoding.ASCII.GetString(textBytes);
        Position += byteCount;

        return true;
    }

    public bool TryReadNullTerminatedAscii(int byteCount, out string? value)
    {
        value = null;

        if (!TryGetBytes(byteCount, out var bytes) || bytes.IsEmpty || bytes[^1] != 0)
        {
            return false;
        }

        var textBytes = bytes[..^1];

        if (textBytes.Contains((byte)0) || !IsAscii(textBytes))
        {
            return false;
        }

        value = Encoding.ASCII.GetString(textBytes);
        Position += byteCount;

        return true;
    }

    public bool TryReadSerial(out Serial value)
    {
        if (!TryReadUInt32BigEndian(out var rawValue))
        {
            value = default;

            return false;
        }

        value = new(rawValue);

        return true;
    }

    public bool TryReadUInt16BigEndian(out ushort value)
    {
        if (Remaining < sizeof(ushort))
        {
            value = default;

            return false;
        }

        value = BinaryPrimitives.ReadUInt16BigEndian(_source[Position..]);
        Position += sizeof(ushort);

        return true;
    }

    public bool TryReadUInt32BigEndian(out uint value)
    {
        if (Remaining < sizeof(uint))
        {
            value = default;

            return false;
        }

        value = BinaryPrimitives.ReadUInt32BigEndian(_source[Position..]);
        Position += sizeof(uint);

        return true;
    }

    public bool TryReadUInt32LittleEndian(out uint value)
    {
        if (Remaining < sizeof(uint))
        {
            value = default;

            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(_source[Position..]);
        Position += sizeof(uint);

        return true;
    }

    private static bool IsAscii(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            if (value > 0x7F)
            {
                return false;
            }
        }

        return true;
    }

    private readonly bool TryGetBytes(int length, out ReadOnlySpan<byte> value)
    {
        if (length < 0 || Remaining < length)
        {
            value = default;

            return false;
        }

        value = _source.Slice(Position, length);

        return true;
    }
}
