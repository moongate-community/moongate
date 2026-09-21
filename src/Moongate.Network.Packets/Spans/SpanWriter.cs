using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Moongate.Core.Primitives;

namespace Moongate.Network.Packets.Spans;

/// <summary>
/// Writes binary packet data into an external or pooled span. Pass pooled writers
/// by reference rather than copying them, and dispose them or transfer ownership
/// with <see cref="ToSpan"/> before leaving their scope.
/// </summary>
public ref struct SpanWriter : IDisposable
{
    public const int AttributeMaximum = 100;

    private readonly bool _resize;
    private byte[]? _arrayToReturnToPool;
    private Span<byte> _buffer;
    private int _position;

    public int BytesWritten { get; private set; }
    public readonly int WrittenCount => BytesWritten;
    public readonly int Remaining => _buffer.Length - _position;
    public readonly Span<byte> WrittenSpan => _buffer[..BytesWritten];

    public int Position
    {
        get => _position;
        private set
        {
            _position = value;

            if (value > BytesWritten)
            {
                BytesWritten = value;
            }
        }
    }

    public readonly int Capacity => _buffer.Length;
    public readonly ReadOnlySpan<byte> Span => _buffer[..BytesWritten];
    public readonly Span<byte> RawBuffer => _buffer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanWriter(Span<byte> initialBuffer, bool resize = false)
    {
        _resize = resize;
        _buffer = initialBuffer;
        _position = 0;
        BytesWritten = 0;
        _arrayToReturnToPool = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanWriter(int initialCapacity, bool resize = false)
    {
        _resize = resize;
        _arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(initialCapacity);
        _buffer = _arrayToReturnToPool;
        _position = 0;
        BytesWritten = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(int count)
    {
        GrowIfNeeded(count);
        _buffer.Slice(_position, count).Clear();
        Position += count;
    }

    /// <summary>
    /// Ensures total buffer capacity; it does not reserve additional bytes after the cursor.
    /// </summary>
    public void EnsureCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        if (capacity > _buffer.Length)
        {
            if (!_resize)
            {
                throw new InvalidOperationException("Insufficient capacity and resizing is disabled.");
            }

            Grow(capacity - BytesWritten);
        }
    }

    public ref byte GetPinnableReference()
    {
        return ref MemoryMarshal.GetReference(_buffer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Grow(int additionalCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(additionalCapacity);
        var requiredCapacity = checked(BytesWritten + additionalCapacity);
        if (!_resize)
        {
            throw new InvalidOperationException("Insufficient capacity and resizing is disabled.");
        }

        var newSize = (int)Math.Max(requiredCapacity, Math.Min(int.MaxValue, (long)_buffer.Length * 2));
        var poolArray = ArrayPool<byte>.Shared.Rent(newSize);
        _buffer.CopyTo(poolArray);

        var toReturn = _arrayToReturnToPool;
        _buffer = _arrayToReturnToPool = poolArray;
        if (toReturn is not null)
        {
            ArrayPool<byte>.Shared.Return(toReturn, clearArray: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Seek(int offset, SeekOrigin origin)
    {
        var offsetBase = origin switch
        {
            SeekOrigin.Begin   => 0L,
            SeekOrigin.Current => (long)_position,
            SeekOrigin.End     => (long)BytesWritten,
            _                  => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        var requested = Math.Max(0L, offsetBase + offset);
        if (requested > int.MaxValue || (requested > Capacity && !_resize))
        {
            throw new IOException("Attempted to seek beyond the available capacity.");
        }

        var newPosition = (int)requested;
        EnsureCapacity(newPosition);
        if (newPosition > BytesWritten)
        {
            _buffer.Slice(BytesWritten, newPosition - BytesWritten).Clear();
        }

        Position = newPosition;
        return Position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToArray()
    {
        if (BytesWritten == 0)
        {
            return Array.Empty<byte>();
        }

        var result = new byte[BytesWritten];
        _buffer[..BytesWritten].CopyTo(result);

        return result;
    }

    /// <summary>
    /// Transfers all written bytes to an owner, copying external buffers, and resets this writer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanOwner ToSpan()
    {
        if (BytesWritten == 0)
        {
            var toReturn = _arrayToReturnToPool;
            this = default;

            if (toReturn is not null)
            {
                ArrayPool<byte>.Shared.Return(toReturn, clearArray: true);
            }

            return new SpanOwner(0, null);
        }

        // Capture the length BEFORE `this = default`, otherwise the reset zeroes
        // the written count and the returned SpanOwner reports length 0.
        var length = BytesWritten;
        var currentPoolBuffer = _arrayToReturnToPool;

        if (currentPoolBuffer is not null)
        {
            this = default;

            return new SpanOwner(length, currentPoolBuffer);
        }

        var ownedBuffer = ArrayPool<byte>.Shared.Rent(length);
        _buffer[..length].CopyTo(ownedBuffer);
        this = default;

        return new SpanOwner(length, ownedBuffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(bool value)
    {
        GrowIfNeeded(1);
        _buffer[Position++] = value ? (byte)1 : (byte)0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(byte value)
    {
        GrowIfNeeded(1);
        _buffer[Position++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(sbyte value)
    {
        GrowIfNeeded(1);
        _buffer[Position++] = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(short value)
    {
        GrowIfNeeded(2);
        BinaryPrimitives.WriteInt16BigEndian(_buffer[_position..], value);
        Position += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort value)
    {
        GrowIfNeeded(2);
        BinaryPrimitives.WriteUInt16BigEndian(_buffer[_position..], value);
        Position += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(int value)
    {
        GrowIfNeeded(4);
        BinaryPrimitives.WriteInt32BigEndian(_buffer[_position..], value);
        Position += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(uint value)
    {
        GrowIfNeeded(4);
        BinaryPrimitives.WriteUInt32BigEndian(_buffer[_position..], value);
        Position += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(long value)
    {
        GrowIfNeeded(8);
        BinaryPrimitives.WriteInt64BigEndian(_buffer[_position..], value);
        Position += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ulong value)
    {
        GrowIfNeeded(8);
        BinaryPrimitives.WriteUInt64BigEndian(_buffer[_position..], value);
        Position += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ReadOnlySpan<byte> buffer)
    {
        var count = buffer.Length;
        var sourceOffset = 0;
        var aliasesBuffer = _arrayToReturnToPool is not null
                            && ((ReadOnlySpan<byte>)_buffer).Overlaps(buffer, out sourceOffset);
        GrowIfNeeded(count);
        if (aliasesBuffer)
        {
            buffer = _buffer.Slice(sourceOffset, count);
        }

        buffer.CopyTo(_buffer[_position..]);
        Position += count;
    }

    /// <summary>
    /// Writes encoded text, optionally truncating the input to <paramref name="fixedLength"/>
    /// UTF-16 code units and padding a fixed field. The field width is fixedLength bytes
    /// for ASCII/UTF-8, twice that for UTF-16, and four times that for UTF-32.
    /// An encoded prefix exceeding the field width is rejected before writing.
    /// </summary>
    public void Write(scoped ReadOnlySpan<char> value, Encoding encoding, int fixedLength = -1)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        ArgumentOutOfRangeException.ThrowIfLessThan(fixedLength, -1);
        var charLength = Math.Min(fixedLength > -1 ? fixedLength : value.Length, value.Length);
        var source = value[..charLength];
        var characterWidth = GetTerminatorWidth(encoding);
        var byteCount = encoding.GetByteCount(source);
        var fixedByteCount = fixedLength < 0 ? -1 : checked(fixedLength * characterWidth);

        if (fixedByteCount >= 0)
        {
            if (byteCount > fixedByteCount)
            {
                throw new ArgumentException("The encoded value exceeds the fixed field width.", nameof(value));
            }

            byteCount = fixedByteCount;
        }

        if (byteCount == 0)
        {
            return;
        }

        char[]? snapshot = null;
        try
        {
            // Growth may return the original array; transcoding can also overwrite
            // unread characters when input and output share an external buffer.
            if (MemoryMarshal.AsBytes(source).Overlaps(_buffer))
            {
                snapshot = ArrayPool<char>.Shared.Rent(source.Length);
                source.CopyTo(snapshot);
                source = snapshot.AsSpan(0, source.Length);
            }

            GrowIfNeeded(byteCount);
            var bytesWritten = encoding.GetBytes(source, _buffer[_position..]);
            Position += bytesWritten;
            if (fixedByteCount > bytesWritten)
            {
                Clear(fixedByteCount - bytesWritten);
            }
        }
        finally
        {
            if (snapshot is not null)
            {
                ArrayPool<char>.Shared.Return(snapshot, clearArray: true);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteAscii(char chr)
    {
        Write((byte)chr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteAscii(string value)
    {
        Write(value.AsSpan(), Encoding.ASCII);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteAscii(string value, int fixedLength)
    {
        Write(value.AsSpan(), Encoding.ASCII, fixedLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteAsciiNull(string value)
    {
        WriteTerminated(value, Encoding.ASCII);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteAttribute(int max, int cur, bool normalize = false, bool reverse = false)
    {
        EnsureRemainingCapacity(2 * sizeof(short));
        if (normalize && max != 0)
        {
            if (reverse)
            {
                Write((short)((long)cur * AttributeMaximum / max));
                Write((short)AttributeMaximum);
            }
            else
            {
                Write((short)AttributeMaximum);
                Write((short)((long)cur * AttributeMaximum / max));
            }
        }
        else
        {
            if (reverse)
            {
                Write((short)cur);
                Write((short)max);
            }
            else
            {
                Write((short)max);
                Write((short)cur);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBigUni(string value)
    {
        Write(value.AsSpan(), Encoding.BigEndianUnicode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBigUni(string value, int fixedLength)
    {
        Write(value.AsSpan(), Encoding.BigEndianUnicode, fixedLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBigUniNull(string value)
    {
        WriteTerminated(value, Encoding.BigEndianUnicode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLE(short value)
    {
        GrowIfNeeded(2);
        BinaryPrimitives.WriteInt16LittleEndian(_buffer[_position..], value);
        Position += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLE(ushort value)
    {
        GrowIfNeeded(2);
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer[_position..], value);
        Position += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLE(int value)
    {
        GrowIfNeeded(4);
        BinaryPrimitives.WriteInt32LittleEndian(_buffer[_position..], value);
        Position += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLE(uint value)
    {
        GrowIfNeeded(4);
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer[_position..], value);
        Position += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLittleUni(string value)
    {
        Write(value.AsSpan(), Encoding.Unicode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLittleUni(string value, int fixedLength)
    {
        Write(value.AsSpan(), Encoding.Unicode, fixedLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLittleUniNull(string value)
    {
        WriteTerminated(value, Encoding.Unicode);
    }

    /// <summary>
    /// Patches the UInt16 length after the opcode, preserving the cursor and written payload.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePacketLength()
    {
        if (BytesWritten is < 3 or > ushort.MaxValue)
        {
            throw new InvalidOperationException("A variable packet must contain a complete header and fit UInt16.");
        }

        BinaryPrimitives.WriteUInt16BigEndian(_buffer[1..], (ushort)BytesWritten);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUTF8(string value)
    {
        Write(value.AsSpan(), Encoding.UTF8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUTF8Null(string value)
    {
        WriteTerminated(value, Encoding.UTF8);
    }

    /// <summary>
    /// Ensures room for a complete write at the current cursor before any bytes are changed.
    /// </summary>
    public void EnsureRemainingCapacity(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        EnsureCapacity(checked(_position + count));
    }

    public void WriteByte(byte value)
    {
        Write(value);
    }

    public void WriteUInt16BigEndian(ushort value)
    {
        Write(value);
    }

    public void WriteUInt32BigEndian(uint value)
    {
        Write(value);
    }

    public void WriteUInt32LittleEndian(uint value)
    {
        WriteLE(value);
    }

    public void WriteSerial(Serial value)
    {
        WriteUInt32BigEndian(value.Value);
    }

    public void WriteBytes(scoped ReadOnlySpan<byte> value)
    {
        Write(value);
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

        EnsureRemainingCapacity(byteCount);
        var field = _buffer.Slice(Position, byteCount);
        field.Clear();
        Encoding.ASCII.GetBytes(value, field);
        Position += byteCount;
    }

    public void WriteNullTerminatedAscii(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateAscii(value);
        EnsureRemainingCapacity(checked(value.Length + 1));
        Encoding.ASCII.GetBytes(value, _buffer[Position..]);
        _buffer[Position + value.Length] = 0;
        Position += value.Length + 1;
    }

    private void WriteTerminated(string value, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(value);
        var terminatorWidth = GetTerminatorWidth(encoding);
        EnsureRemainingCapacity(checked(encoding.GetByteCount(value) + terminatorWidth));
        Write(value.AsSpan(), encoding);
        Clear(terminatorWidth);
    }

    private static int GetTerminatorWidth(Encoding encoding)
    {
        return encoding switch
        {
            UnicodeEncoding => 2,
            UTF32Encoding   => 4,
            _               => 1
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GrowIfNeeded(int count)
    {
        EnsureRemainingCapacity(count);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        var toReturn = _arrayToReturnToPool;
        this = default;

        if (toReturn is not null)
        {
            ArrayPool<byte>.Shared.Return(toReturn, clearArray: true);
        }
    }
}
