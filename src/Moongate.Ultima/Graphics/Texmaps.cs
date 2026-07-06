using Moongate.Ultima.Imaging;
using Moongate.Ultima.Io;

namespace Moongate.Ultima.Graphics;

/// <summary>
/// Reads texmaps.mul / texidx.mul: the terrain textures used to render land tiles.
/// Each texture is a raw (non-RLE) square of 16-bit ARGB1555 pixels, either 64x64 or
/// 128x128, inferred from the entry length. Ported from the UOFiddler Ultima library.
/// </summary>
public static class Texmaps
{
    private const int TextureCount = 0x4000;
    private const int SmallTextureSize = 64;
    private const int LargeTextureSize = 128;
    private const int LargeTextureLength = LargeTextureSize * LargeTextureSize * 2;

    private static FileIndex _fileIndex = new("texidx.mul", "texmaps.mul", TextureCount, 10);

    private static UltimaBitmap[] _cache = new UltimaBitmap[TextureCount];
    private static bool[] _removed = new bool[TextureCount];

    /// <summary>Number of texture entries in the index.</summary>
    public static int GetCount()
    {
        return (int)(_fileIndex.IdxLength / 12);
    }

    /// <summary>Returns true when <paramref name="index" /> resolves to a stored texture.</summary>
    public static bool IsValidIndex(int index)
    {
        if (index < 0 || index >= TextureCount || _removed[index])
        {
            return false;
        }

        return _fileIndex.Valid(index, out var length, out var _, out var _) && length > 0;
    }

    /// <summary>Returns the texture <paramref name="index" /> as an opaque bitmap, or null when absent.</summary>
    public static unsafe UltimaBitmap GetTexmap(int index)
    {
        if (index < 0 || index >= TextureCount || _removed[index])
        {
            return null;
        }

        if (_cache[index] != null)
        {
            return _cache[index];
        }

        var stream = _fileIndex.Seek(index, out var length, out var _, out var _);

        if (stream == null || length <= 0)
        {
            return null;
        }

        var size = length >= LargeTextureLength ? LargeTextureSize : SmallTextureSize;

        var buffer = new byte[length];
        stream.ReadExactly(buffer, 0, length);

        var bmp = new UltimaBitmap(size, size);
        var line = (ushort*)bmp.Scan0;
        var delta = bmp.Stride >> 1;

        fixed (byte* data = buffer)
        {
            var source = (ushort*)data;

            for (var y = 0; y < size; ++y, line += delta)
            {
                var cur = line;

                for (var x = 0; x < size; ++x)
                {
                    cur[x] = (ushort)(*source++ | 0x8000);
                }
            }
        }

        _cache[index] = bmp;

        return bmp;
    }

    /// <summary>Returns the raw 16-bit pixel bytes of texture <paramref name="index" /> and its square side.</summary>
    public static byte[] GetRawTexmap(int index, out int size)
    {
        size = 0;

        if (index < 0 || index >= TextureCount || _removed[index])
        {
            return null;
        }

        var stream = _fileIndex.Seek(index, out var length, out var _, out var _);

        if (stream == null || length <= 0)
        {
            return null;
        }

        size = length >= LargeTextureLength ? LargeTextureSize : SmallTextureSize;

        var buffer = new byte[length];
        stream.ReadExactly(buffer, 0, length);

        return buffer;
    }

    /// <summary>Marks texture <paramref name="index" /> as removed so later reads return null.</summary>
    public static void Remove(int index)
    {
        if (index >= 0 && index < TextureCount)
        {
            _removed[index] = true;
        }
    }

    /// <summary>Rebuilds the index against the current client directory and clears the cache.</summary>
    public static void Reload()
    {
        _fileIndex = new("texidx.mul", "texmaps.mul", TextureCount, 10);
        _cache = new UltimaBitmap[TextureCount];
        _removed = new bool[TextureCount];
    }
}
