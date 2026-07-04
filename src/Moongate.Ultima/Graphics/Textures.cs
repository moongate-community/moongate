using System.Buffers;
using Moongate.Ultima.Helpers;
using Moongate.Ultima.Imaging;
using Moongate.Ultima.Io;

namespace Moongate.Ultima.Graphics;

public sealed class Textures
{
    private static FileIndex _fileIndex = new("Texidx.mul", "Texmaps.mul", 0x4000, 10);
    private static UltimaBitmap[] _cache = new UltimaBitmap[0x4000];
    private static bool[] _removed = new bool[0x4000];
    private static readonly Dictionary<int, bool> _patched = new();

    private struct Checksums
    {
        public int Position;
        public int Length;
        public int Extra;
    }

    public static int GetIdxLength()
        => (int)(_fileIndex.IdxLength / 12);

    /// <summary>
    /// Returns Bitmap of Texture
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public static UltimaBitmap GetTexture(int index)
        => GetTexture(index, out var _);

    /// <summary>
    /// Returns Bitmap of Texture with verdata bool
    /// </summary>
    /// <param name="index"></param>
    /// <param name="patched"></param>
    /// <returns></returns>
    public static unsafe UltimaBitmap GetTexture(int index, out bool patched)
    {
        patched = _patched.ContainsKey(index) && _patched[index];

        if (_removed[index])
        {
            return null;
        }

        if (_cache[index] != null)
        {
            return _cache[index];
        }

        var stream = _fileIndex.Seek(index, out var length, out var extra, out patched);

        if (stream == null)
        {
            return null;
        }

        if (length == 0)
        {
            return null;
        }

        if (patched)
        {
            _patched[index] = true;
        }

        var size = extra == 0 ? 64 : 128;

        var max = size * size * 2;

        var streamBuffer = ArrayPool<byte>.Shared.Rent(max);

        try
        {
            stream.ReadExactly(streamBuffer, 0, max);

            var bmp = new UltimaBitmap(size, size);

            {
                var line = (ushort*)bmp.Scan0;
                var delta = bmp.Stride >> 1;

                fixed (byte* data = streamBuffer)
                {
                    var binData = (ushort*)data;

                    for (var y = 0; y < size; ++y, line += delta)
                    {
                        var cur = line;
                        var end = cur + size;

                        while (cur < end)
                        {
                            *cur++ = (ushort)(*binData++ ^ 0x8000);
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
            ArrayPool<byte>.Shared.Return(streamBuffer);
        }
    }

    /// <summary>
    /// ReReads texmaps
    /// </summary>
    public static void Reload()
    {
        _fileIndex = new("Texidx.mul", "Texmaps.mul", 0x4000, 10);
        _cache = new UltimaBitmap[0x4000];
        _removed = new bool[0x4000];
        _patched.Clear();
    }

    /// <summary>
    /// Removes Texture <see cref="_removed" />
    /// </summary>
    /// <param name="index"></param>
    public static void Remove(int index)
        => _removed[index] = true;

    /// <summary>
    /// Replaces Texture
    /// </summary>
    /// <param name="index"></param>
    /// <param name="bmp"></param>
    public static void Replace(int index, UltimaBitmap bmp)
    {
        _cache[index] = bmp;
        _removed[index] = false;
        _patched.Remove(index);
    }

    public static unsafe void Save(string path)
    {
        var idx = Path.Combine(path, "texidx.mul");
        var mul = Path.Combine(path, "texmaps.mul");

        // M3.5: xxHash128-keyed dedup index, replacing the old
        // List<Checksums>+SHA256-bytes layout and its O(n²) linear scan.
        var checksums = new Dictionary<UInt128, Checksums>();

        var memIdx = new MemoryStream();
        var memMul = new MemoryStream();

        using (var binIdx = new BinaryWriter(memIdx))
        {
            using (var binMul = new BinaryWriter(memMul))
            {
                for (var index = 0; index < GetIdxLength(); ++index)
                {
                    if (_cache[index] == null)
                    {
                        _cache[index] = GetTexture(index);
                    }

                    var bmp = _cache[index];

                    if (bmp == null || _removed[index])
                    {
                        binIdx.Write(0); // lookup
                        binIdx.Write(0); // length
                        binIdx.Write(0); // extra
                    }
                    else
                    {
                        {
                            var hash = bmp.Hash128();

                            if (checksums.TryGetValue(hash, out var existing))
                            {
                                binIdx.Write(existing.Position); // lookup
                                binIdx.Write(existing.Length);   // length
                                binIdx.Write(existing.Extra);    // extra

                                continue;
                            }

                            var line = (ushort*)bmp.Scan0;
                            var delta = bmp.Stride >> 1;

                            binIdx.Write((int)binMul.BaseStream.Position); // lookup
                            var length = (int)binMul.BaseStream.Position;

                            for (var y = 0; y < bmp.Height; ++y, line += delta)
                            {
                                var cur = line;

                                for (var x = 0; x < bmp.Width; ++x)
                                {
                                    binMul.Write((ushort)(cur[x] ^ 0x8000));
                                }
                            }

                            var start = length;
                            length = (int)binMul.BaseStream.Position - length;
                            binIdx.Write(length);
                            var extra = GetExtraFlag(length);
                            binIdx.Write(extra);

                            checksums[hash] = new()
                            {
                                Position = start,
                                Length = length,
                                Extra = extra
                            };
                        }
                    }
                }

                using (var fileIdx = new FileStream(idx, FileMode.Create, FileAccess.Write, FileShare.Write))
                {
                    using (var fileMul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write))
                    {
                        memIdx.WriteTo(fileIdx);
                        memMul.WriteTo(fileMul);
                    }
                }
            }
        }

        memIdx.Dispose();
    }

    /// <summary>
    /// Tests if index is valid Texture
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public static bool TestTexture(int index)
    {
        index &= 0x3FFF;

        if (_removed[index])
        {
            return false;
        }

        if (_cache[index] != null)
        {
            return true;
        }

        var valid = _fileIndex.Valid(index, out var length, out var _, out var _);

        return valid && length != 0;
    }

    private static int GetExtraFlag(int length)

        // length of 0x8000 == width 128x128 else 64x64
        => length == 0x8000 ? 1 : 0;
}
