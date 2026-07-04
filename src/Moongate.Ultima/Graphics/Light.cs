using System.Buffers;
using Moongate.Ultima.Imaging;
using Moongate.Ultima.Io;

namespace Moongate.Ultima.Graphics;

public sealed class Light
{
    private static FileIndex _fileIndex = new("lightidx.mul", "light.mul", 100, -1);
    private static UltimaBitmap[] _cache = new UltimaBitmap[100];
    private static bool[] _removed = new bool[100];

    /// <summary>
    /// Gets count of defined lights
    /// </summary>
    /// <returns></returns>
    public static int GetCount()
    {
        var idxPath = Files.GetFilePath("lightidx.mul");

        if (idxPath == null)
        {
            return 0;
        }

        using (var index = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            return (int)(index.Length / 12);
        }
    }

    /// <summary>
    /// Returns Bitmap of given index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public static unsafe UltimaBitmap GetLight(int index)
    {
        if (_removed[index])
        {
            return null;
        }

        if (_cache[index] != null)
        {
            return _cache[index];
        }

        var stream = _fileIndex.Seek(index, out var length, out var extra, out var _);

        if (stream == null)
        {
            return null;
        }

        var width = extra & 0xFFFF;
        var height = (extra >> 16) & 0xFFFF;

        var buffer = ArrayPool<byte>.Shared.Rent(length);

        try
        {
            stream.ReadExactly(buffer, 0, length);

            var bmp = new UltimaBitmap(width, height);

            {
                var line = (ushort*)bmp.Scan0;
                var delta = bmp.Stride >> 1;

                fixed (byte* data = buffer)
                {
                    var bindat = (sbyte*)data;

                    for (var y = 0; y < height; ++y, line += delta)
                    {
                        var cur = line;
                        var end = cur + width;

                        while (cur < end)
                        {
                            var value = *bindat++;
                            *cur++ = (ushort)(((0x1f + value) << 10) + ((0x1F + value) << 5) + 0x1F + value);
                        }
                    }
                }
            }

            if (!Files.CacheData)
            {
                return _cache[index] = bmp;
            }

            return bmp;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static byte[] GetRawLight(int index, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (_removed[index])
        {
            return null;
        }

        var stream = _fileIndex.Seek(index, out var length, out var extra, out var _);

        if (stream == null)
        {
            return null;
        }

        width = extra & 0xFFFF;
        height = (extra >> 16) & 0xFFFF;
        var buffer = new byte[length];
        stream.ReadExactly(buffer, 0, length);

        return buffer;
    }

    /// <summary>
    /// ReReads light.mul
    /// </summary>
    public static void Reload()
    {
        _fileIndex = new("lightidx.mul", "light.mul", 100, -1);
        _cache = new UltimaBitmap[100];
        _removed = new bool[100];
    }

    /// <summary>
    /// Removes Light <see cref="_removed" />
    /// </summary>
    /// <param name="index"></param>
    public static void Remove(int index)
        => _removed[index] = true;

    /// <summary>
    /// Replaces Light
    /// </summary>
    /// <param name="index"></param>
    /// <param name="bmp"></param>
    public static void Replace(int index, UltimaBitmap bmp)
    {
        _cache[index] = bmp;
        _removed[index] = false;
    }

    public static unsafe void Save(string path)
    {
        var idx = Path.Combine(path, "lightidx.mul");
        var mul = Path.Combine(path, "light.mul");

        using (var fsidx = new FileStream(idx, FileMode.Create, FileAccess.Write, FileShare.Write))
        {
            using (var fsmul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                using (var binidx = new BinaryWriter(fsidx))
                {
                    using (var binmul = new BinaryWriter(fsmul))
                    {
                        for (var index = 0; index < _cache.Length; index++)
                        {
                            if (_cache[index] == null)
                            {
                                _cache[index] = GetLight(index);
                            }

                            var bmp = _cache[index];

                            if (bmp == null || _removed[index])
                            {
                                // TODO: check what should be here because ServUO version has the version below
                                /*
                    binidx.Write(-1); // lookup
                    binidx.Write(-1); // length
                    binidx.Write(-1); // extra
                     */
                                binidx.Write(-1); // lookup
                                binidx.Write(0);  // length
                                binidx.Write(0);  // extra
                            }
                            else
                            {
                                var line = (ushort*)bmp.Scan0;
                                var delta = bmp.Stride >> 1;

                                binidx.Write((int)fsmul.Position); //lookup
                                var length = (int)fsmul.Position;

                                for (var y = 0; y < bmp.Height; ++y, line += delta)
                                {
                                    var cur = line;
                                    var end = cur + bmp.Width;

                                    while (cur < end)
                                    {
                                        // TODO: maybe this below will be better replacement? Needs checking. It comes from ServUO Ultima.dll version
                                        /*
                            var value = (sbyte)(((*cur++ >> 10) & 0xffff) - 0x1f);
                            if (value > 0) // wtf? but it works...
                            {
                                --value;
                            }

                            binmul.Write(value);
                            */

                                        var ccur = *cur++;
                                        sbyte value = 0;

                                        if (ccur > 0) // Zero should stay zero cause it means transparence
                                        {
                                            value = (sbyte)(((ccur >> 10) & 0xffff) - 0x1f);
                                        }

                                        if (value > 0) // wtf? but it works...
                                        {
                                            --value;
                                        }

                                        binmul.Write(value);
                                    }
                                }

                                length = (int)fsmul.Position - length;
                                binidx.Write(length);
                                binidx.Write((bmp.Height << 16) + bmp.Width); // TODO: first should be bmp.Width?
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Tests if given index is valid
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public static bool TestLight(int index)
    {
        if (_removed[index])
        {
            return false;
        }

        if (_cache[index] != null)
        {
            return true;
        }

        var stream = _fileIndex.Seek(index, out var _, out var extra, out var _);

        if (stream == null)
        {
            return false;
        }

        var width = extra & 0xFFFF;
        var height = (extra >> 16) & 0xFFFF;

        return width > 0 && height > 0;
    }
}
