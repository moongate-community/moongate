using System.Buffers;

namespace Moongate.Core.Buffers;

/// <summary>
/// Wrapper for char buffers rented from ArrayPool&lt;char&gt;.Shared that will be used in InterpolatedStringHandlers.
/// The wrapper prevents intermediate strings from being created unnecessarily.
/// Note: TryFormat can only be called once. Using the PooledArraySpanFormattable after calling TryFormat will throw.
/// To use the span multiple times, use the Chars property directly instead.
/// </summary>
public struct PooledArraySpanFormattable : ISpanFormattable, IDisposable
{
    private readonly int _pos;
    private char[] _arrayToReturnToPool;
    private string _value;

    public ReadOnlySpan<char> Chars => _arrayToReturnToPool.AsSpan(.._pos);

    public PooledArraySpanFormattable(char[] arrayToReturnToPool, int length)
    {
        _arrayToReturnToPool = arrayToReturnToPool;
        _pos = length;
        _value = null;
    }

    public string ToString(string? format = null, IFormatProvider formatProvider = null)
    {
        _value ??= new(_arrayToReturnToPool.AsSpan(0, _pos));

        if (_arrayToReturnToPool is not null)
        {
            ArrayPool<char>.Shared.Return(_arrayToReturnToPool);
            _arrayToReturnToPool = null;
        }

        // We don't dispose so we can call ToString() multiple times with idempotence.
        return _value;
    }

    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider provider = null
    )
    {
        if (destination.Length < _pos)
        {
            charsWritten = 0;

            return false;
        }

        _arrayToReturnToPool.AsSpan(0, _pos).CopyTo(destination);
        charsWritten = _pos;

        // Interpolated string handlers do not dispose, but we need to return the chars to the array.
        Dispose();

        return true;
    }

    public static implicit operator string(PooledArraySpanFormattable f)
        => f.ToString();

    public void Dispose()
    {
        if (_arrayToReturnToPool is not null)
        {
            ArrayPool<char>.Shared.Return(_arrayToReturnToPool);
        }

        this = default; // Defensive clear
    }
}
