using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Interfaces.Framing;

namespace Moongate.Network.Buffers.Internal;

internal sealed class PendingFrameBuffer : IDisposable
{
    private readonly int _budget;
    private readonly INetFramer _framer;
    private readonly int _maxFrameLength;
    private readonly ArrayPool<byte> _pool;
    private readonly int _receiveBufferSize;
    private byte[]? _buffer;
    private int _disposed;
    private int _length;

    public int Length => _length;

    public PendingFrameBuffer(
        INetFramer framer,
        int receiveBufferSize,
        int maxFrameLength,
        ArrayPool<byte>? pool = null
    )
    {
        ArgumentNullException.ThrowIfNull(framer);

        if (receiveBufferSize is < 1 or > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(receiveBufferSize));
        }

        if (maxFrameLength is < 1 or > 16 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrameLength));
        }

        _framer = framer;
        _receiveBufferSize = receiveBufferSize;
        _maxFrameLength = maxFrameLength;
        _pool = pool ?? ArrayPool<byte>.Shared;
        _budget = checked(maxFrameLength + receiveBufferSize);
    }

    public void Append(ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();

        if (data.IsEmpty)
        {
            return;
        }

        if (data.Length > _budget - _length)
        {
            throw new InvalidDataException("Pending TCP data exceeds the configured buffer budget.");
        }

        var required = checked(_length + data.Length);
        EnsureCapacity(required);
        data.CopyTo(_buffer.AsSpan(_length, data.Length));
        _length = required;
    }

    public bool TryRead([NotNullWhen(true)] out byte[]? frame)
    {
        ThrowIfDisposed();
        frame = null;

        if (_length == 0)
        {
            return false;
        }

        var view = _buffer.AsSpan(0, _length);

        if (!_framer.TryReadFrame(view, out var frameLength))
        {
            if (_length > _maxFrameLength)
            {
                throw new InvalidDataException(
                    $"Incoming frame exceeds the maximum of {_maxFrameLength} bytes."
                );
            }

            return false;
        }

        if (frameLength <= 0 || frameLength > _length)
        {
            throw new InvalidDataException(
                $"Framer reported an invalid frame length of {frameLength} bytes for {_length} pending bytes."
            );
        }

        if (frameLength > _maxFrameLength)
        {
            throw new InvalidDataException(
                $"Incoming frame of {frameLength} bytes exceeds the maximum of {_maxFrameLength} bytes."
            );
        }

        frame = view[..frameLength].ToArray();
        Consume(frameLength);
        return true;
    }

    private void Consume(int count)
    {
        var remaining = _length - count;

        if (remaining > 0)
        {
            _buffer.AsSpan(count, remaining).CopyTo(_buffer);
        }

        _length = remaining;
    }

    private void EnsureCapacity(int required)
    {
        if (_buffer is null)
        {
            var initialCapacity = Math.Min(_budget, Math.Max(_receiveBufferSize, required));
            _buffer = _pool.Rent(initialCapacity);
            return;
        }

        if (required <= _buffer.Length)
        {
            return;
        }

        var doubledCapacity = _buffer.Length > _budget / 2 ? _budget : _buffer.Length * 2;
        var requestedCapacity = Math.Min(_budget, Math.Max(required, doubledCapacity));
        var expanded = _pool.Rent(requestedCapacity);
        _buffer.AsSpan(0, _length).CopyTo(expanded);
        _pool.Return(_buffer);
        _buffer = expanded;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var buffer = _buffer;
        _buffer = null;
        _length = 0;

        if (buffer is not null)
        {
            _pool.Return(buffer);
        }
    }
}
