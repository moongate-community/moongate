using System.Buffers.Binary;
using System.Text;
using Moongate.Ultima.Helpers;

namespace Moongate.Tests.Support;

/// <summary>
/// Builds minimal synthetic UO client files (old, pre-HS formats) for reader tests.
/// Layouts mirror the on-disk structures parsed by Moongate.Ultima.
/// </summary>
public static class UltimaFixtures
{
    private const int OldLandRecordSize = 26;
    private const int OldItemRecordSize = 37;
    private const int NewLandRecordSize = 30;
    private const int NewItemRecordSize = 41;
    private const int LandGroupSize = 4 + 32 * OldLandRecordSize;
    private const int ItemGroupSize = 4 + 32 * OldItemRecordSize;
    private const int NewLandGroupSize = 4 + 32 * NewLandRecordSize;
    private const int NewItemGroupSize = 4 + 32 * NewItemRecordSize;
    private const int MapBlockSize = 196;

    /// <summary>
    /// Minimum artidx.mul size that makes <c>Art.IsUOAHS()</c> report the post-HS client
    /// (0x13FDC entries of 12 bytes), which switches TileData to the new 64-bit-flag format.
    /// </summary>
    private const int UoahsArtIdxSize = 0x13FDC * 12;

    /// <summary>
    /// Builds a hues.mul with a single 708-byte block (8 hues). The first hue gets the
    /// provided name, colors, and table range; the rest stay zeroed.
    /// </summary>
    public static byte[] BuildHues(string firstHueName, ushort firstColor, ushort tableStart, ushort tableEnd)
    {
        var buffer = new byte[708];

        var offset = 4;

        for (var c = 0; c < 32; c++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset + c * 2), firstColor);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset + 64), tableStart);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset + 66), tableEnd);
        WriteName(buffer, offset + 68, firstHueName);

        return buffer;
    }

    /// <summary>
    /// Builds a single-block (8x8) map0.mul where every cell has the given land id and z.
    /// </summary>
    public static byte[] BuildMapBlock(ushort landId, sbyte z)
    {
        var buffer = new byte[MapBlockSize];

        for (var cell = 0; cell < 64; cell++)
        {
            var offset = 4 + cell * 3;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), landId);
            buffer[offset + 2] = unchecked((byte)z);
        }

        return buffer;
    }

    /// <summary>
    /// Wraps raw map block data into a minimal MythicPackage (UOP) container the way
    /// the client ships mapXLegacyMUL.uop: a single uncompressed
    /// <c>build/{pattern}/00000000.dat</c> entry holding the blocks.
    /// </summary>
    /// <param name="pattern">Inner entry pattern, e.g. "map0legacymul".</param>
    /// <param name="blockData">Concatenated 196-byte map blocks.</param>
    public static byte[] BuildMapUop(string pattern, byte[] blockData)
    {
        const int headerSize = 28;
        const int blockTableHeaderSize = 12;
        const int entrySize = 34;
        var dataOffset = headerSize + blockTableHeaderSize + entrySize;

        var buffer = new byte[dataOffset + blockData.Length];
        Span<byte> span = buffer;

        // Header: magic "MYP\0", version+signature, first block offset, capacity, file count.
        BinaryPrimitives.WriteInt32LittleEndian(span, 0x50594D);
        BinaryPrimitives.WriteInt64LittleEndian(span[4..], 0);
        BinaryPrimitives.WriteInt64LittleEndian(span[12..], headerSize);
        BinaryPrimitives.WriteInt32LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], 1);

        // Block table: one entry, no next block.
        BinaryPrimitives.WriteInt32LittleEndian(span[headerSize..], 1);
        BinaryPrimitives.WriteInt64LittleEndian(span[(headerSize + 4)..], 0);

        // Entry: offset, headerLength, compressed/decompressed length, name hash, adler, flag 0 (raw).
        var entry = headerSize + blockTableHeaderSize;
        BinaryPrimitives.WriteInt64LittleEndian(span[entry..], dataOffset);
        BinaryPrimitives.WriteInt32LittleEndian(span[(entry + 8)..], 0);
        BinaryPrimitives.WriteInt32LittleEndian(span[(entry + 12)..], blockData.Length);
        BinaryPrimitives.WriteInt32LittleEndian(span[(entry + 16)..], blockData.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(
            span[(entry + 20)..],
            UopUtils.HashFileName($"build/{pattern}/00000000.dat")
        );
        BinaryPrimitives.WriteUInt32LittleEndian(span[(entry + 28)..], 0);
        BinaryPrimitives.WriteInt16LittleEndian(span[(entry + 32)..], 0);

        blockData.CopyTo(buffer, dataOffset);

        return buffer;
    }

    /// <summary>
    /// Builds staidx0.mul + statics0.mul for a single block containing the given statics.
    /// Each static: (id, cellX, cellY, z, hue) with cell coordinates inside the block (0-7).
    /// </summary>
    public static (byte[] Index, byte[] Statics) BuildStatics(
        params (ushort Id, byte CellX, byte CellY, sbyte Z, ushort Hue)[] statics
    )
    {
        var data = new byte[statics.Length * 7];

        for (var i = 0; i < statics.Length; i++)
        {
            var offset = i * 7;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), statics[i].Id);
            data[offset + 2] = statics[i].CellX;
            data[offset + 3] = statics[i].CellY;
            data[offset + 4] = unchecked((byte)statics[i].Z);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 5), statics[i].Hue);
        }

        var index = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(0), 0);
        BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(4), data.Length);
        BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(8), 0);

        return (index, data);
    }

    /// <summary>
    /// Builds an old-format tiledata.mul: the full 0x4000 land table plus one item group
    /// (32 items). Land and item entries are zeroed except the ones set via the callbacks.
    /// </summary>
    public static byte[] BuildTileData(
        Action<int, uint, ushort, string, byte[]> setLand,
        Action<int, uint, string, byte[]> setItem
    )
    {
        var buffer = new byte[512 * LandGroupSize + ItemGroupSize];

        setLand(0, 0, 0, string.Empty, buffer);
        setItem(0, 0, string.Empty, buffer);

        return buffer;
    }

    /// <summary>Builds an old-format tiledata.mul with the full land table and one item group.</summary>
    public static byte[] BuildTileData()
        => new byte[512 * LandGroupSize + ItemGroupSize];

    /// <summary>Builds a new-format (HS 7.0.9+) tiledata.mul with the full land table and one item group.</summary>
    public static byte[] BuildTileDataNew()
        => new byte[512 * NewLandGroupSize + NewItemGroupSize];

    /// <summary>
    /// Builds a zero-filled artidx.mul large enough that the library detects a post-HS
    /// client (new tiledata format). Pair with <see cref="BuildTileDataNew" />.
    /// </summary>
    public static byte[] BuildUoahsArtIndex()
        => new byte[UoahsArtIdxSize];

    /// <summary>
    /// Creates a temporary directory holding the given synthetic client files and
    /// returns its path. Caller deletes it when done.
    /// </summary>
    public static string CreateClientDirectory(params (string Name, byte[] Content)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("moongate-uo-fixture-").FullName;

        foreach (var (name, content) in files)
        {
            File.WriteAllBytes(Path.Combine(dir, name), content);
        }

        return dir;
    }

    /// <summary>Writes an old-format item record (flags, anim, height, name) for item <paramref name="id" /> (first group only).</summary>
    public static void SetItem(byte[] tileData, int id, uint flags, byte height, string name, short anim = 0)
    {
        var offset = 512 * LandGroupSize + 4 + id * OldItemRecordSize;

        BinaryPrimitives.WriteUInt32LittleEndian(tileData.AsSpan(offset), flags);
        BinaryPrimitives.WriteInt16LittleEndian(tileData.AsSpan(offset + 10), anim);
        tileData[offset + 16] = height;
        WriteName(tileData, offset + 17, name);
    }

    /// <summary>Writes a new-format item record (64-bit flags, height, name) for item <paramref name="id" /> (first group only).</summary>
    public static void SetItemNew(byte[] tileData, int id, ulong flags, byte height, string name)
    {
        var offset = 512 * NewLandGroupSize + 4 + id * NewItemRecordSize;

        BinaryPrimitives.WriteUInt64LittleEndian(tileData.AsSpan(offset), flags);
        tileData[offset + 20] = height;
        WriteName(tileData, offset + 21, name);
    }

    /// <summary>Writes an old-format land record (flags, textureId, name) for tile <paramref name="id" />.</summary>
    public static void SetLand(byte[] tileData, int id, uint flags, ushort textureId, string name)
    {
        var group = id / 32;
        var inGroup = id % 32;
        var offset = group * LandGroupSize + 4 + inGroup * OldLandRecordSize;

        BinaryPrimitives.WriteUInt32LittleEndian(tileData.AsSpan(offset), flags);
        BinaryPrimitives.WriteUInt16LittleEndian(tileData.AsSpan(offset + 4), textureId);
        WriteName(tileData, offset + 6, name);
    }

    /// <summary>Writes a new-format land record (64-bit flags, textureId, name) for tile <paramref name="id" />.</summary>
    public static void SetLandNew(byte[] tileData, int id, ulong flags, ushort textureId, string name)
    {
        var group = id / 32;
        var inGroup = id % 32;
        var offset = group * NewLandGroupSize + 4 + inGroup * NewLandRecordSize;

        BinaryPrimitives.WriteUInt64LittleEndian(tileData.AsSpan(offset), flags);
        BinaryPrimitives.WriteUInt16LittleEndian(tileData.AsSpan(offset + 8), textureId);
        WriteName(tileData, offset + 10, name);
    }

    /// <summary>
    /// Builds a verdata.mul: int32 patch count followed by 20-byte records
    /// (file, index, lookup, length, extra).
    /// </summary>
    public static byte[] BuildVerdata(params (int File, int Index, int Lookup, int Length, int Extra)[] patches)
    {
        var buffer = new byte[4 + patches.Length * 20];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, patches.Length);

        for (var i = 0; i < patches.Length; i++)
        {
            var offset = 4 + i * 20;
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), patches[i].File);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset + 4), patches[i].Index);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset + 8), patches[i].Lookup);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset + 12), patches[i].Length);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset + 16), patches[i].Extra);
        }

        return buffer;
    }

    /// <summary>Overwrites one cell of a single-block map built by <see cref="BuildMapBlock" />.</summary>
    public static void SetMapCell(byte[] mapBlock, int x, int y, ushort landId, sbyte z)
    {
        var offset = 4 + (((y & 0x7) << 3) + (x & 0x7)) * 3;
        BinaryPrimitives.WriteUInt16LittleEndian(mapBlock.AsSpan(offset), landId);
        mapBlock[offset + 2] = unchecked((byte)z);
    }

    /// <summary>
    /// Builds artidx.mul + art.mul holding a single solid-color static at
    /// <paramref name="itemId" /> (record 0x4000 + itemId). All other records are -1.
    /// </summary>
    public static (byte[] Index, byte[] Art) BuildStaticArt(int itemId, int width, int height, ushort color)
    {
        var pixels = new List<ushort>
        {
            0,
            0,
            (ushort)width,
            (ushort)height
        };

        var rowLength = 2 + width + 2; // offset+run, pixels, terminator pair
        var stored = (ushort)(color ^ 0x8000); // the reader XORs every pixel with 0x8000

        for (var y = 0; y < height; y++)
        {
            pixels.Add((ushort)(y * rowLength));
        }

        for (var y = 0; y < height; y++)
        {
            pixels.Add(0);              // xOffset
            pixels.Add((ushort)width);  // xRun

            for (var x = 0; x < width; x++)
            {
                pixels.Add(stored);
            }

            pixels.Add(0);              // terminator offset
            pixels.Add(0);              // terminator run
        }

        var art = new byte[pixels.Count * 2];

        for (var i = 0; i < pixels.Count; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(art.AsSpan(i * 2), pixels[i]);
        }

        var recordCount = 0x4000 + itemId + 1;
        var index = new byte[recordCount * 12];
        index.AsSpan().Fill(0xFF); // every lookup/length = -1

        var record = (0x4000 + itemId) * 12;
        BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(record), 0);
        BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(record + 4), art.Length);
        BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(record + 8), 0);

        return (index, art);
    }

    /// <summary>Builds gumpidx.mul + gumpart.mul containing solid-color gumps.</summary>
    public static (byte[] Index, byte[] Gumps) BuildGumps(params (int GumpId, int Width, int Height, ushort Color)[] gumps)
    {
        var maxId = gumps.Max(g => g.GumpId);
        var index = new byte[(maxId + 1) * 12];
        index.AsSpan().Fill(0xFF);

        var data = new List<byte>();

        foreach (var (gumpId, width, height, color) in gumps)
        {
            var lookup = data.Count;
            var blob = new List<byte>();

            for (var y = 0; y < height; y++)
            {
                // row offset in DWORDs relative to blob start: lookup table (height) + y rows of 1 dword each
                var rowOffset = height + y;
                blob.AddRange(BitConverter.GetBytes(rowOffset));
            }

            // real gump data stores colors with bit15 clear; the reader XORs with 0x8000,
            // which sets the alpha bit and makes the pixel opaque for DrawInto
            var stored = (ushort)(color & 0x7FFF);

            for (var y = 0; y < height; y++)
            {
                blob.AddRange(BitConverter.GetBytes(stored));
                blob.AddRange(BitConverter.GetBytes((ushort)width));
            }

            data.AddRange(blob);

            var record = gumpId * 12;
            BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(record), lookup);
            BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(record + 4), blob.Count);
            BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(record + 8), (width << 16) | height);
        }

        return (index, data.ToArray());
    }

    /// <summary>
    /// Builds anim.idx + anim.mul with one animation of <paramref name="frameCount" />
    /// identical solid frames for (body &lt; 200, action, direction &lt;= 4), fileType 1.
    /// </summary>
    public static (byte[] Index, byte[] Anim) BuildAnim(
        int body, int action, int direction, int frameCount, int width, int height,
        byte paletteIndex, ushort paletteColor)
    {
        const int DoubleXor = (0x200 << 22) | (0x200 << 12);

        var blob = new List<byte>();

        for (var i = 0; i < 256; i++)
        {
            var value = i == paletteIndex ? (ushort)(paletteColor ^ 0x8000) : (ushort)0x8000;
            blob.AddRange(BitConverter.GetBytes(value));
        }

        var frame = new List<byte>();
        frame.AddRange(BitConverter.GetBytes((short)0x200));            // xCenter
        frame.AddRange(BitConverter.GetBytes((short)(0x200 - height))); // yCenter -> origin at (0,0)
        frame.AddRange(BitConverter.GetBytes((ushort)width));
        frame.AddRange(BitConverter.GetBytes((ushort)height));

        for (var y = 0; y < height; y++)
        {
            var wanted = (0 << 22) | (y << 12) | width;
            frame.AddRange(BitConverter.GetBytes(wanted ^ DoubleXor));

            for (var x = 0; x < width; x++)
            {
                frame.Add(paletteIndex);
            }
        }

        frame.AddRange(BitConverter.GetBytes(0x7FFF7FFF));

        // header after palette: frameCount + frameCount offsets (relative to that point)
        var tableBytes = 4 + frameCount * 4;
        blob.AddRange(BitConverter.GetBytes(frameCount));

        for (var i = 0; i < frameCount; i++)
        {
            blob.AddRange(BitConverter.GetBytes(tableBytes + i * frame.Count));
        }

        for (var i = 0; i < frameCount; i++)
        {
            blob.AddRange(frame);
        }

        var recordIndex = body * 110 + action * 5 + direction;
        var index = new byte[(recordIndex + 1) * 12];
        index.AsSpan().Fill(0xFF);

        var record = recordIndex * 12;
        BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(record), 0);
        BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(record + 4), blob.Count);
        BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(record + 8), 0);

        return (index, blob.ToArray());
    }

    private static void WriteName(byte[] buffer, int offset, string name)
    {
        var bytes = Encoding.ASCII.GetBytes(name);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, 20));
    }
}
