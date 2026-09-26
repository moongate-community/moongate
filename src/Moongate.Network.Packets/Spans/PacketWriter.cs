using System.Buffers.Binary;
using System.Text;
using Moongate.Core.Primitives;

namespace Moongate.Network.Packets.Spans;

public ref struct PacketWriter
{
    private readonly Span<byte> _destination;

    public int WrittenCount { get; private set; }
    public readonly int Remaining => _destination.Length - WrittenCount;
    public readonly Span<byte> WrittenSpan => _destination[..WrittenCount];

    public PacketWriter(Span<byte> destination)
    {
        _destination = destination;
        WrittenCount = 0;
    }

    public readonly void EnsureCapacity(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (Remaining < count)
        {
            throw new InvalidOperationException("The destination does not have enough remaining capacity.");
        }
    }

    public void WriteByte(byte value)
    {
        EnsureCapacity(1);
        _destination[WrittenCount] = value;
        WrittenCount++;
    }

    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        EnsureCapacity(value.Length);
        value.CopyTo(_destination[WrittenCount..]);
        WrittenCount += value.Length;
    }

    public void WriteFixedAscii(string value, int byteCount)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);
        ValidateAscii(value);

        if (value.Length > byteCount)
        {
            throw new ArgumentException("The value exceeds the fixed ASCII field width.", nameof(value));
        }

        EnsureCapacity(byteCount);
        var field = _destination.Slice(WrittenCount, byteCount);
        field.Clear();
        Encoding.ASCII.GetBytes(value, field);
        WrittenCount += byteCount;
    }

    public void WriteNullTerminatedAscii(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateAscii(value);
        EnsureCapacity(checked(value.Length + 1));
        Encoding.ASCII.GetBytes(value, _destination[WrittenCount..]);
        _destination[WrittenCount + value.Length] = 0;
        WrittenCount += value.Length + 1;
    }

    public void WriteSerial(Serial value)
    {
        WriteUInt32BigEndian(value.Value);
    }

    public void WriteUInt16BigEndian(ushort value)
    {
        EnsureCapacity(sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(_destination[WrittenCount..], value);
        WrittenCount += sizeof(ushort);
    }

    public void WriteUInt32BigEndian(uint value)
    {
        EnsureCapacity(sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(_destination[WrittenCount..], value);
        WrittenCount += sizeof(uint);
    }

    public void WriteUInt32LittleEndian(uint value)
    {
        EnsureCapacity(sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(_destination[WrittenCount..], value);
        WrittenCount += sizeof(uint);
    }

    private static void ValidateAscii(string value)
    {
        foreach (var character in value)
        {
            if (character is '\0' or > '\x7F')
            {
                throw new ArgumentException("The value must contain only non-NUL ASCII characters.", nameof(value));
            }
        }
    }
}
