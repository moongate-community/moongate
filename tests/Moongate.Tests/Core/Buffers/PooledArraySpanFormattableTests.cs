using System.Buffers;
using Moongate.Core.Buffers;

namespace Moongate.Tests.Core.Buffers;

public class PooledArraySpanFormattableTests
{
    [Fact]
    public void ToString_RepeatedThenDisposed_PreservesCachedText()
    {
        var array = ArrayPool<char>.Shared.Rent(5);
        "hello".AsSpan().CopyTo(array);
        var value = new PooledArraySpanFormattable(array, 5);

        try
        {
            Assert.Equal("hello", value.ToString());
            Assert.Equal("hello", value.ToString());
        }
        finally
        {
            value.Dispose();
            value.Dispose();
        }
    }

    [Fact]
    public void TryFormat_InsufficientDestination_CanRetryBeforeDisposal()
    {
        var array = ArrayPool<char>.Shared.Rent(5);
        "hello".AsSpan().CopyTo(array);
        var value = new PooledArraySpanFormattable(array, 5);

        try
        {
            Assert.False(value.TryFormat(stackalloc char[4], out var tooSmall));
            Assert.Equal(0, tooSmall);
            Assert.Equal("hello", value.Chars.ToString());

            Span<char> destination = stackalloc char[5];
            Assert.True(value.TryFormat(destination, out var written));
            Assert.Equal(5, written);
            Assert.Equal("hello", destination.ToString());
        }
        finally
        {
            value.Dispose();
        }
    }

    [Fact]
    public void Dispose_DefaultValue_CanBeRepeated()
    {
        var value = default(PooledArraySpanFormattable);

        value.Dispose();
        value.Dispose();

        Assert.True(value.Chars.IsEmpty);
    }
}
