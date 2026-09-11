using System.IO.Hashing;
using Moongate.Ultima.Imaging;

namespace Moongate.Ultima.Helpers;

public static class Extensions
{
    /// <summary>
    /// Hashes a bitmap's pixel data using xxHash128, allocation-free over the
    /// native ARGB1555 buffer. Used for Save-time deduplication: the 128-bit
    /// struct is a perfect Dictionary key for O(1) dedup. Not a cryptographic
    /// hash; do not use for security-sensitive comparisons.
    /// </summary>
    public static unsafe UInt128 Hash128(this UltimaBitmap bmp)
    {
        ArgumentNullException.ThrowIfNull(bmp);

        var size = bmp.Stride * bmp.Height;
        var span = new ReadOnlySpan<byte>((void*)bmp.Scan0, size);

        return XxHash128.HashToUInt128(span);
    }

    /// <summary>
    /// Copies the raw ARGB1555 pixel data of <paramref name="bmp" /> into a byte array.
    /// </summary>
    public static unsafe byte[] ToArray(this UltimaBitmap bmp)
    {
        ArgumentNullException.ThrowIfNull(bmp);

        var size = bmp.Stride * bmp.Height;
        var buffer = new byte[size];
        new ReadOnlySpan<byte>((void*)bmp.Scan0, size).CopyTo(buffer);

        return buffer;
    }
}
