using System.Buffers;
using Moongate.Ultima.Caching;
using Moongate.Ultima.Helpers;
using Moongate.Ultima.Imaging;
using Moongate.Ultima.Interfaces;
using Moongate.Ultima.Io;
using Moongate.Ultima.Types;

namespace Moongate.Ultima.Graphics;

public sealed class Gumps
{
    private static FileIndex _fileIndex = new(
        "Gumpidx.mul",
        "Gumpart.mul",
        "gumpartLegacyMUL.uop",
        0xFFFF,
        12,
        ".tga",
        -1,
        true
    );

    // LRU read cache replaces the old Bitmap[_fileIndex.IndexLength].
    // User edits go in _replaced (below) and are never evicted.
    private static LruBitmapCache _cache;
    private static readonly Dictionary<int, UltimaBitmap> _replaced = new();
    private static bool[] _removed;
    private static readonly Dictionary<int, bool> _patched = new();

    private static byte[] _pixelBuffer;
    private static byte[] _streamBuffer;
    private static byte[] _colorTable;

    // Authoritative id range — what _cache.Length used to be before the
    // LRU swap. Sourced from the FileIndex when available, falls back to
    // 0xFFFF (the gump id space ceiling) when no client is configured.
    private static int _indexLength;

    static Gumps()
    {
        _cache = new(Files.CacheCapacityGumps);
        _indexLength = _fileIndex?.IndexLength > 0 ? (int)_fileIndex.IndexLength : 0xFFFF;
        _removed = new bool[_indexLength];
    }

    public static int GetCount()
        => _indexLength;

    /// <summary>
    /// Returns Bitmap of index and applies Hue
    /// </summary>
    /// <param name="index"></param>
    /// <param name="hue"></param>
    /// <param name="onlyHueGrayPixels"></param>
    /// <param name="patched"></param>
    /// <returns></returns>

    // TODO: Currently unused and may be broken because of recent UOP changes. Needs verdata `patched` checks and compression handling
    public static unsafe UltimaBitmap GetGump(int index, Hue hue, bool onlyHueGrayPixels, out bool patched)
    {
        var stream = _fileIndex.Seek(index, out var length, out var extra, out patched);

        if (stream == null)
        {
            return null;
        }

        if (extra == -1)
        {
            return null;
        }

        var width = (extra >> 16) & 0xFFFF;
        var height = extra & 0xFFFF;

        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var bytesPerLine = width << 1;
        var bytesPerStride = (bytesPerLine + 3) & ~3;
        var bytesForImage = height * bytesPerStride;

        var pixelsPerStride = (width + 1) & ~1;
        var pixelsPerStrideDelta = pixelsPerStride - width;

        var pixelBuffer = _pixelBuffer;

        if (pixelBuffer == null || pixelBuffer.Length < bytesForImage)
        {
            _pixelBuffer = pixelBuffer = new byte[(bytesForImage + 2047) & ~2047];
        }

        var streamBuffer = _streamBuffer;

        if (streamBuffer == null || streamBuffer.Length < length)
        {
            _streamBuffer = streamBuffer = new byte[(length + 2047) & ~2047];
        }

        var colorTable = _colorTable;

        if (colorTable == null)
        {
            _colorTable = colorTable = new byte[128];
        }

        stream.ReadExactly(streamBuffer, 0, length);

        fixed (ushort* psHueColors = hue.Colors)
        {
            fixed (byte* pbStream = streamBuffer)
            {
                fixed (byte* pbPixels = pixelBuffer)
                {
                    fixed (byte* pbColorTable = colorTable)
                    {
                        var pHueColors = psHueColors;
                        var pHueColorsEnd = pHueColors + 32;

                        var pColorTable = (ushort*)pbColorTable;

                        var pColorTableOpaque = pColorTable;

                        while (pHueColors < pHueColorsEnd)
                        {
                            *pColorTableOpaque++ = *pHueColors++;
                        }

                        var pPixelDataStart = (ushort*)pbPixels;

                        var pLookup = (int*)pbStream;
                        var pLookupEnd = pLookup + height;
                        var pPixelRleStart = pLookup;
                        int* pPixelRle;

                        var pPixel = pPixelDataStart;
                        ushort* pRleEnd;
                        var pPixelEnd = pPixel + width;

                        ushort color,
                               count;

                        if (onlyHueGrayPixels)
                        {
                            while (pLookup < pLookupEnd)
                            {
                                pPixelRle = pPixelRleStart + *pLookup++;
                                pRleEnd = pPixel;

                                while (pPixel < pPixelEnd)
                                {
                                    color = *(ushort*)pPixelRle;
                                    count = *(1 + (ushort*)pPixelRle);
                                    ++pPixelRle;

                                    pRleEnd += count;

                                    if (color != 0 &&
                                        (color & 0x1F) == ((color >> 5) & 0x1F) &&
                                        (color & 0x1F) == ((color >> 10) & 0x1F))
                                    {
                                        color = pColorTable[color >> 10];
                                    }
                                    else if (color != 0)
                                    {
                                        color ^= 0x8000;
                                    }

                                    while (pPixel < pRleEnd)
                                    {
                                        *pPixel++ = color;
                                    }
                                }

                                pPixel += pixelsPerStrideDelta;
                                pPixelEnd += pixelsPerStride;
                            }
                        }
                        else
                        {
                            while (pLookup < pLookupEnd)
                            {
                                pPixelRle = pPixelRleStart + *pLookup++;
                                pRleEnd = pPixel;

                                while (pPixel < pPixelEnd)
                                {
                                    color = *(ushort*)pPixelRle;
                                    count = *(1 + (ushort*)pPixelRle);
                                    ++pPixelRle;

                                    pRleEnd += count;

                                    if (color != 0)
                                    {
                                        color = pColorTable[color >> 10];
                                    }

                                    while (pPixel < pRleEnd)
                                    {
                                        *pPixel++ = color;
                                    }
                                }

                                pPixel += pixelsPerStrideDelta;
                                pPixelEnd += pixelsPerStride;
                            }
                        }

                        // The GDI+ version aliased the shared _pixelBuffer via the
                        // pointer Bitmap constructor; UltimaBitmap owns its memory,
                        // so copy the decoded rows (buffer stride: pixelsPerStride).
                        var result = new UltimaBitmap(width, height);
                        var pDest = (ushort*)result.Scan0;

                        for (var y = 0; y < height; ++y)
                        {
                            Buffer.MemoryCopy(
                                pPixelDataStart + y * pixelsPerStride,
                                pDest + y * width,
                                bytesPerLine,
                                bytesPerLine
                            );
                        }

                        return result;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns Bitmap of index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public static UltimaBitmap GetGump(int index)
        => GetGump(index, out _);

    /// <summary>
    /// Returns Bitmap of index and if verdata patched
    /// </summary>
    /// <param name="index"></param>
    /// <param name="patched"></param>
    /// <returns></returns>
    public static unsafe UltimaBitmap GetGump(int index, out bool patched)
    {
        patched = _patched.ContainsKey(index) && _patched[index];

        if (index > _indexLength - 1)
        {
            return null;
        }

        if (_removed[index])
        {
            return null;
        }

        if (_replaced.TryGetValue(index, out var replaced))
        {
            return replaced;
        }

        if (_cache.TryGet(index, out var cached))
        {
            return cached;
        }

        IEntry entry = null;
        var stream = _fileIndex.Seek(index, ref entry, out patched);

        if (stream == null || entry == null)
        {
            return null;
        }

        if (entry.Extra1 == -1)
        {
            return null;
        }

        if (patched)
        {
            _patched[index] = true;
        }

        var length = entry.Length;

        if (patched)
        {
            length = entry.Length & 0x7FFFFFFF;
        }

        var rented = ArrayPool<byte>.Shared.Rent(length);
        byte[] zlibBuf = null;
        byte[] mythicBuf = null;

        try
        {
            stream.ReadExactly(rented, 0, length);

            var data = rented;
            var dataOffset = 0;

            var width = (uint)entry.Extra1;
            var height = (uint)entry.Extra2;

            // Compressed UOPs
            if (entry.Flag >= CompressionFlagType.Zlib)
            {
                var decSize = entry.DecompressedLength;

                if (decSize <= 8)
                {
                    return null;
                }

                zlibBuf = ArrayPool<byte>.Shared.Rent(decSize);

                if (!UopUtils.TryDecompressInto(rented, 0, length, zlibBuf, out var zlibLen))
                {
                    return null;
                }

                if (entry.Flag == CompressionFlagType.Mythic)
                {
                    var mythicLen = MythicDecompress.PeekDecompressedLength(zlibBuf.AsSpan(0, zlibLen));

                    if (mythicLen <= 8 || mythicLen > int.MaxValue)
                    {
                        return null;
                    }

                    mythicBuf = ArrayPool<byte>.Shared.Rent((int)mythicLen);

                    if (!MythicDecompress.TryDecompress(
                            zlibBuf.AsSpan(0, zlibLen),
                            mythicBuf.AsSpan(0, (int)mythicLen),
                            out _
                        ))
                    {
                        return null;
                    }

                    data = mythicBuf;
                }
                else
                {
                    data = zlibBuf;
                }

                width = (uint)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24));
                height = (uint)(data[4] | (data[5] << 8) | (data[6] << 16) | (data[7] << 24));
                dataOffset = 8;

                entry.Extra1 = (int)width;
                entry.Extra2 = (int)height;
            }

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            try
            {
                var bmp = new UltimaBitmap((int)width, (int)height);

                fixed (byte* dataPtr = data)
                {
                    var basePtr = dataPtr + dataOffset;
                    var lookup = (int*)basePtr;
                    var dat = (ushort*)basePtr;

                    var line = (ushort*)bmp.Scan0;
                    var delta = bmp.Stride >> 1;

                    for (var y = 0; y < (int)height; ++y, line += delta)
                    {
                        var count = *lookup++ * 2;

                        var cur = line;
                        var end = line + bmp.Width;

                        while (cur < end)
                        {
                            var color = dat[count++];
                            var next = cur + dat[count++];

                            if (color == 0)
                            {
                                cur = next;
                            }
                            else
                            {
                                color ^= 0x8000;

                                while (cur < next)
                                {
                                    *cur++ = color;
                                }
                            }
                        }
                    }
                }

                if (Files.CacheData)
                {
                    _cache.Set(index, bmp);
                }

                return bmp;
            }
            catch (Exception)
            {
                // ignored
                return null;
            }
        }
        finally
        {
            if (mythicBuf != null)
            {
                ArrayPool<byte>.Shared.Return(mythicBuf);
            }

            if (zlibBuf != null)
            {
                ArrayPool<byte>.Shared.Return(zlibBuf);
            }

            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public static byte[] GetRawGump(int index, out int width, out int height)
    {
        width = -1;
        height = -1;

        IEntry entry = null;
        var stream = _fileIndex.Seek(index, ref entry, out var patched);

        if (stream == null || entry == null)
        {
            return null;
        }

        if (entry.Extra1 == -1)
        {
            return null;
        }

        // Compressed UOPs
        if (entry.Flag >= CompressionFlagType.Zlib)
        {
            if (patched)
            {
                throw new InvalidOperationException("Verdata.mul is not supported for compressed UOP");
            }

            var compLen = entry.Length;
            var decSize = entry.DecompressedLength;

            if (decSize <= 8)
            {
                return null;
            }

            var rented = ArrayPool<byte>.Shared.Rent(compLen);
            var zlibBuf = ArrayPool<byte>.Shared.Rent(decSize);
            byte[] mythicBuf = null;

            try
            {
                stream.ReadExactly(rented, 0, compLen);

                if (!UopUtils.TryDecompressInto(rented, 0, compLen, zlibBuf, out var zlibLen))
                {
                    return null;
                }

                byte[] payload;
                int payloadLength;

                if (entry.Flag == CompressionFlagType.Mythic)
                {
                    var mythicLen = MythicDecompress.PeekDecompressedLength(zlibBuf.AsSpan(0, zlibLen));

                    if (mythicLen <= 8 || mythicLen > int.MaxValue)
                    {
                        return null;
                    }

                    mythicBuf = ArrayPool<byte>.Shared.Rent((int)mythicLen);

                    if (!MythicDecompress.TryDecompress(
                            zlibBuf.AsSpan(0, zlibLen),
                            mythicBuf.AsSpan(0, (int)mythicLen),
                            out _
                        ))
                    {
                        return null;
                    }

                    payload = mythicBuf;
                    payloadLength = (int)mythicLen;
                }
                else
                {
                    payload = zlibBuf;
                    payloadLength = zlibLen;
                }

                width = (payload[3] << 24) | (payload[2] << 16) | (payload[1] << 8) | payload[0];
                height = (payload[7] << 24) | (payload[6] << 16) | (payload[5] << 8) | payload[4];
                entry.Extra1 = width;
                entry.Extra2 = height;

                if (width <= 0 || height <= 0)
                {
                    return null;
                }

                // Returned array holds the payload without the 8-byte header.
                var resultLen = payloadLength - 8;
                var result = new byte[resultLen];
                Buffer.BlockCopy(payload, 8, result, 0, resultLen);

                return result;
            }
            finally
            {
                if (mythicBuf != null)
                {
                    ArrayPool<byte>.Shared.Return(mythicBuf);
                }

                ArrayPool<byte>.Shared.Return(zlibBuf);
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        width = entry.Extra1;
        height = entry.Extra2;

        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var length = entry.Length;

        if (patched)
        {
            length = entry.Length & 0x7FFFFFFF;
        }

        var buffer = new byte[length];
        stream.ReadExactly(buffer, 0, length);

        return buffer;
    }

    /// <summary>
    /// Tests if index is defined
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public static bool IsValidIndex(int index)
    {
        if (_fileIndex == null)
        {
            return false;
        }

        if (index > _indexLength - 1)
        {
            return false;
        }

        if (_removed[index])
        {
            return false;
        }

        if (_replaced.ContainsKey(index) || _cache.TryGet(index, out _))
        {
            return true;
        }

        if (!_fileIndex.Valid(index, out _, out var extra, out _))
        {
            return false;
        }

        if (extra == -1)
        {
            return false;
        }

        var width = (extra >> 16) & 0xFFFF;
        var height = extra & 0xFFFF;

        return width > 0 && height > 0;
    }

    /// <summary>
    /// Preloads all gumps in parallel, populating the LRU bitmap cache.
    /// Each worker opens its own FileStream against the .uop / .mul so the
    /// expensive part — zlib + Mythic decompression and RLE decode — runs
    /// concurrently across CPU cores. Per-bitmap work is unchanged; only
    /// the orchestration is parallel.
    /// Set <paramref name="parallelism" /> to 0 to use ProcessorCount.
    /// <paramref name="progressCallback" /> is invoked from worker threads
    /// with the cumulative count of completed gumps; the caller is
    /// responsible for marshalling to the UI thread if needed.
    /// </summary>
    public static void PreloadParallel(int parallelism, Action<int> progressCallback)
    {
        if (_fileIndex?.FileAccessor == null)
        {
            return;
        }

        var mulPath = _fileIndex.MulPath;

        if (string.IsNullOrEmpty(mulPath) || !File.Exists(mulPath))
        {
            return;
        }

        var total = _indexLength;

        if (total <= 0)
        {
            return;
        }

        if (parallelism <= 0)
        {
            parallelism = Environment.ProcessorCount;
        }

        var done = 0;
        var reportEvery = Math.Max(1, total / 200);
        var nextReport = reportEvery;
        Lock reportLock = new();

        var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism };

        Parallel.For(
            0,
            total,
            options,
            () => new FileStream(mulPath, FileMode.Open, FileAccess.Read, FileShare.Read),
            (index, _, stream) =>
            {
                DecodeAndCacheOne(index, stream);

                var doneNow = Interlocked.Increment(ref done);

                if (progressCallback != null && doneNow >= Volatile.Read(ref nextReport))
                {
                    var shouldReport = false;

                    lock (reportLock)
                    {
                        if (doneNow >= nextReport)
                        {
                            nextReport = doneNow + reportEvery;
                            shouldReport = true;
                        }
                    }

                    if (shouldReport)
                    {
                        progressCallback(doneNow);
                    }
                }

                return stream;
            },
            stream => stream?.Dispose()
        );

        progressCallback?.Invoke(total);
    }

    /// <summary>
    /// ReReads gumpart
    /// </summary>
    public static void Reload()
    {
        try
        {
            _fileIndex?.Dispose();
            _fileIndex = new("Gumpidx.mul", "Gumpart.mul", "gumpartLegacyMUL.uop", 0xFFFF, 12, ".tga", -1, true);
            _indexLength = _fileIndex.IndexLength > 0 ? (int)_fileIndex.IndexLength : 0xFFFF;
            _cache?.Clear();
            _cache ??= new(Files.CacheCapacityGumps);
            _replaced.Clear();
            _removed = new bool[_indexLength];
        }
        catch
        {
            _fileIndex = null;
            _indexLength = 0xFFFF;
            _cache?.Clear();
            _cache ??= new(Files.CacheCapacityGumps);
            _replaced.Clear();
            _removed = new bool[_indexLength];
        }

        //_pixelBuffer = null;
        _streamBuffer = null;

        //_colorTable = null;
        _patched.Clear();
    }

    /// <summary>
    /// Removes Gumpindex <see cref="_removed" />
    /// </summary>
    /// <param name="index"></param>
    public static void RemoveGump(int index)
        => _removed[index] = true;

    /// <summary>
    /// Replaces Gump <see cref="_replaced" />
    /// </summary>
    /// <param name="index"></param>
    /// <param name="bmp"></param>
    public static void ReplaceGump(int index, UltimaBitmap bmp)
    {
        _replaced[index] = bmp;
        _cache.Remove(index);
        _removed[index] = false;
        _patched.Remove(index);
    }

    public static unsafe void Save(string path)
    {
        var idx = Path.Combine(path, "Gumpidx.mul");
        var mul = Path.Combine(path, "Gumpart.mul");

        using (var fsidx = new FileStream(idx, FileMode.Create, FileAccess.Write, FileShare.Write))
        {
            using (var fsmul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                using (var binidx = new BinaryWriter(fsidx))
                {
                    using (var binmul = new BinaryWriter(fsmul))
                    {
                        for (var index = 0; index < _indexLength; index++)
                        {
                            Files.FireFileSaveEvent();

                            // GetGump transparently checks _replaced first, then the
                            // LRU cache, then decodes from disk.
                            var bmp = GetGump(index);

                            if (bmp == null || _removed[index])
                            {
                                binidx.Write(-1); // lookup
                                binidx.Write(0);  // length
                                binidx.Write(0);  // extra
                            }
                            else
                            {
                                var line = (ushort*)bmp.Scan0;
                                var delta = bmp.Stride >> 1;

                                binidx.Write((int)fsmul.Position); // lookup
                                var length = (int)fsmul.Position;
                                const int fill = 0;

                                for (var i = 0; i < bmp.Height; ++i)
                                {
                                    binmul.Write(fill);
                                }

                                for (var y = 0; y < bmp.Height; ++y, line += delta)
                                {
                                    var cur = line;

                                    var x = 0;
                                    var current = (int)fsmul.Position;
                                    fsmul.Seek(length + y * 4, SeekOrigin.Begin);
                                    var offset = (current - length) / 4;
                                    binmul.Write(offset);
                                    fsmul.Seek(length + offset * 4, SeekOrigin.Begin);

                                    while (x < bmp.Width)
                                    {
                                        var run = 1;
                                        var c = cur[x];

                                        while (x + run < bmp.Width)
                                        {
                                            if (c != cur[x + run])
                                            {
                                                break;
                                            }

                                            ++run;
                                        }

                                        if (c == 0)
                                        {
                                            binmul.Write(c);
                                        }
                                        else
                                        {
                                            binmul.Write((ushort)(c ^ 0x8000));
                                        }

                                        binmul.Write((short)run);
                                        x += run;
                                    }
                                }

                                length = (int)fsmul.Position - length;
                                binidx.Write(length);
                                binidx.Write((bmp.Width << 16) + bmp.Height);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Override the LRU cap for the Gumps read cache. See
    /// <see cref="Files.CacheCapacityGumps" /> for the default.
    /// </summary>
    public static void SetCacheCapacity(int capacity)
        => _cache.SetCapacity(capacity);

    /// <summary>
    /// Returns the dimensions of a gump without decoding pixel data.
    /// Cheaper than TryGetGumpPixels when the caller only needs to size a
    /// destination buffer. For UOP-compressed entries we still have to
    /// decompress to recover width/height — those are paid for on the
    /// first hit and cached via entry.Extra1/Extra2.
    /// </summary>
    public static bool TryGetGumpDimensions(int index, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (index < 0 || index >= _indexLength || _fileIndex.FileAccessor == null)
        {
            return false;
        }

        var entry = _fileIndex[index];

        if (entry.Lookup < 0 || entry.Extra1 == -1)
        {
            return false;
        }

        // For uncompressed entries the index already knows width/height.
        if (entry.Flag < CompressionFlagType.Zlib)
        {
            width = entry.Extra1;
            height = entry.Extra2;

            return width > 0 && height > 0;
        }

        // Compressed entries need a one-shot decode to recover dims.
        // Falls through to TryGetGumpPixels with a 0-length destination
        // which returns false but populates width/height.
        return TryGetGumpPixels(index, Span<ushort>.Empty, out width, out height, out _);
    }

    /// <summary>
    /// Decodes a gump into a caller-supplied pixel buffer. Lets the caller
    /// reuse a single shared destination across many decodes (e.g. a
    /// listview rendering thumbnails) instead of paying the per-call
    /// `new Bitmap(...)` + GDI handle + LockBits cost.
    /// `destination` must be at least <paramref name="width" /> *
    /// <paramref name="height" /> ushorts; the buffer is filled with
    /// Format16bppArgb1555 pixels. Returns false if the gump is missing,
    /// removed, the entry has invalid dimensions, or the buffer is too
    /// small. width / height are out parameters and are filled even when
    /// the buffer is too small, so callers can resize and retry.
    /// Cache semantics: this method does not write to or read from
    /// _cache — every call decodes from disk. Use GetGump when you want
    /// the standard bitmap cache.
    /// </summary>
    public static unsafe bool TryGetGumpPixels(
        int index,
        Span<ushort> destination,
        out int width,
        out int height,
        out bool patched
    )
    {
        width = 0;
        height = 0;
        patched = _patched.ContainsKey(index) && _patched[index];

        if (index < 0 || index > _indexLength - 1)
        {
            return false;
        }

        if (_removed[index])
        {
            return false;
        }

        IEntry entry = null;
        var stream = _fileIndex.Seek(index, ref entry, out patched);

        if (stream == null || entry == null || entry.Extra1 == -1)
        {
            return false;
        }

        if (patched)
        {
            _patched[index] = true;
        }

        var length = entry.Length;

        if (patched)
        {
            length = entry.Length & 0x7FFFFFFF;
        }

        var rented = ArrayPool<byte>.Shared.Rent(length);
        byte[] zlibBuf = null;
        byte[] mythicBuf = null;

        try
        {
            stream.ReadExactly(rented, 0, length);

            var data = rented;
            var dataOffset = 0;
            width = entry.Extra1;
            height = entry.Extra2;

            if (entry.Flag >= CompressionFlagType.Zlib)
            {
                var decSize = entry.DecompressedLength;

                if (decSize <= 8)
                {
                    return false;
                }

                zlibBuf = ArrayPool<byte>.Shared.Rent(decSize);

                if (!UopUtils.TryDecompressInto(rented, 0, length, zlibBuf, out var zlibLen))
                {
                    return false;
                }

                if (entry.Flag == CompressionFlagType.Mythic)
                {
                    var mythicLen = MythicDecompress.PeekDecompressedLength(zlibBuf.AsSpan(0, zlibLen));

                    if (mythicLen <= 8 || mythicLen > int.MaxValue)
                    {
                        return false;
                    }

                    mythicBuf = ArrayPool<byte>.Shared.Rent((int)mythicLen);

                    if (!MythicDecompress.TryDecompress(
                            zlibBuf.AsSpan(0, zlibLen),
                            mythicBuf.AsSpan(0, (int)mythicLen),
                            out _
                        ))
                    {
                        return false;
                    }

                    data = mythicBuf;
                }
                else
                {
                    data = zlibBuf;
                }

                // Header: 4-byte width then 4-byte height (little-endian), pixel data at offset 8.
                width = data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24);
                height = data[4] | (data[5] << 8) | (data[6] << 16) | (data[7] << 24);
                dataOffset = 8;
                entry.Extra1 = width;
                entry.Extra2 = height;
            }

            if (width <= 0 || height <= 0 || destination.Length < width * height)
            {
                return false;
            }

            fixed (byte* dataPtr = data)
            {
                fixed (ushort* destPtr = destination)
                {
                    var basePtr = dataPtr + dataOffset;
                    var lookup = (int*)basePtr;
                    var dat = (ushort*)basePtr;

                    for (var y = 0; y < height; ++y)
                    {
                        var count = *lookup++ * 2;

                        var cur = destPtr + y * width;
                        var end = cur + width;

                        while (cur < end)
                        {
                            var color = dat[count++];
                            var next = cur + dat[count++];

                            if (color == 0)
                            {
                                cur = next;
                            }
                            else
                            {
                                color ^= 0x8000;

                                while (cur < next)
                                {
                                    *cur++ = color;
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }
        finally
        {
            if (mythicBuf != null)
            {
                ArrayPool<byte>.Shared.Return(mythicBuf);
            }

            if (zlibBuf != null)
            {
                ArrayPool<byte>.Shared.Return(zlibBuf);
            }
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static unsafe void DecodeAndCacheOne(int index, FileStream stream)
    {
        if (_removed[index] || _replaced.ContainsKey(index))
        {
            return;
        }

        // Cheap precheck — if the cache already has it (e.g. from a prior
        // single-shot GetGump), don't redecode.
        if (_cache.TryGet(index, out _))
        {
            return;
        }

        var entry = _fileIndex[index];

        if (entry == null || entry.Lookup < 0 || entry.Extra1 == -1)
        {
            return;
        }

        var length = entry.Length & 0x7FFFFFFF;

        if (length <= 0)
        {
            return;
        }

        var rented = ArrayPool<byte>.Shared.Rent(length);
        byte[] zlibBuf = null;
        byte[] mythicBuf = null;

        try
        {
            stream.Seek(entry.Lookup, SeekOrigin.Begin);
            stream.ReadExactly(rented, 0, length);

            var data = rented;
            var dataOffset = 0;
            var width = (uint)entry.Extra1;
            var height = (uint)entry.Extra2;

            if (entry.Flag >= CompressionFlagType.Zlib)
            {
                var decSize = entry.DecompressedLength;

                if (decSize <= 8)
                {
                    return;
                }

                zlibBuf = ArrayPool<byte>.Shared.Rent(decSize);

                if (!UopUtils.TryDecompressInto(rented, 0, length, zlibBuf, out var zlibLen))
                {
                    return;
                }

                if (entry.Flag == CompressionFlagType.Mythic)
                {
                    var mythicLen = MythicDecompress.PeekDecompressedLength(zlibBuf.AsSpan(0, zlibLen));

                    if (mythicLen <= 8 || mythicLen > int.MaxValue)
                    {
                        return;
                    }

                    mythicBuf = ArrayPool<byte>.Shared.Rent((int)mythicLen);

                    if (!MythicDecompress.TryDecompress(
                            zlibBuf.AsSpan(0, zlibLen),
                            mythicBuf.AsSpan(0, (int)mythicLen),
                            out _
                        ))
                    {
                        return;
                    }

                    data = mythicBuf;
                }
                else
                {
                    data = zlibBuf;
                }

                width = (uint)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24));
                height = (uint)(data[4] | (data[5] << 8) | (data[6] << 16) | (data[7] << 24));
                dataOffset = 8;
            }

            if (width == 0 || height == 0 || width > 0xFFFF || height > 0xFFFF)
            {
                return;
            }

            UltimaBitmap bmp;

            try
            {
                bmp = new((int)width, (int)height);
            }
            catch
            {
                return;
            }

            fixed (byte* dataPtr = data)
            {
                var basePtr = dataPtr + dataOffset;
                var lookup = (int*)basePtr;
                var dat = (ushort*)basePtr;

                var line = (ushort*)bmp.Scan0;
                var delta = bmp.Stride >> 1;

                for (var y = 0; y < (int)height; ++y, line += delta)
                {
                    var count = *lookup++ * 2;

                    var cur = line;
                    var end = line + bmp.Width;

                    while (cur < end)
                    {
                        var color = dat[count++];
                        var next = cur + dat[count++];

                        if (color == 0)
                        {
                            cur = next;
                        }
                        else
                        {
                            color ^= 0x8000;

                            while (cur < next)
                            {
                                *cur++ = color;
                            }
                        }
                    }
                }
            }

            if (Files.CacheData)
            {
                _cache.Set(index, bmp);
            }
        }
        catch
        {
            // Skip this index; preload should not abort the whole sweep.
        }
        finally
        {
            if (mythicBuf != null)
            {
                ArrayPool<byte>.Shared.Return(mythicBuf);
            }

            if (zlibBuf != null)
            {
                ArrayPool<byte>.Shared.Return(zlibBuf);
            }

            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
