using System.Buffers.Binary;
using System.Text;

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
    private const int LandGroupSize = 4 + (32 * OldLandRecordSize);
    private const int ItemGroupSize = 4 + (32 * OldItemRecordSize);
    private const int NewLandGroupSize = 4 + (32 * NewLandRecordSize);
    private const int NewItemGroupSize = 4 + (32 * NewItemRecordSize);
    private const int MapBlockSize = 196;

    /// <summary>
    /// Minimum artidx.mul size that makes <c>Art.IsUOAHS()</c> report the post-HS client
    /// (0x13FDC entries of 12 bytes), which switches TileData to the new 64-bit-flag format.
    /// </summary>
    private const int UoahsArtIdxSize = 0x13FDC * 12;

    /// <summary>
    /// Creates a temporary directory holding the given synthetic client files and
    /// returns its path. Caller deletes it when done.
    /// </summary>
    public static string CreateClientDirectory(params (string Name, byte[] Content)[] files)
    {
        string dir = Directory.CreateTempSubdirectory("moongate-uo-fixture-").FullName;

        foreach ((string name, byte[] content) in files)
        {
            File.WriteAllBytes(Path.Combine(dir, name), content);
        }

        return dir;
    }

    /// <summary>
    /// Builds an old-format tiledata.mul: the full 0x4000 land table plus one item group
    /// (32 items). Land and item entries are zeroed except the ones set via the callbacks.
    /// </summary>
    public static byte[] BuildTileData(
        Action<int, uint, ushort, string, byte[]> setLand,
        Action<int, uint, string, byte[]> setItem)
    {
        var buffer = new byte[(512 * LandGroupSize) + ItemGroupSize];

        setLand(0, 0, 0, string.Empty, buffer);
        setItem(0, 0, string.Empty, buffer);

        return buffer;
    }

    /// <summary>Builds an old-format tiledata.mul with the full land table and one item group.</summary>
    public static byte[] BuildTileData()
    {
        return new byte[(512 * LandGroupSize) + ItemGroupSize];
    }

    /// <summary>Builds a new-format (HS 7.0.9+) tiledata.mul with the full land table and one item group.</summary>
    public static byte[] BuildTileDataNew()
    {
        return new byte[(512 * NewLandGroupSize) + NewItemGroupSize];
    }

    /// <summary>
    /// Builds a zero-filled artidx.mul large enough that the library detects a post-HS
    /// client (new tiledata format). Pair with <see cref="BuildTileDataNew"/>.
    /// </summary>
    public static byte[] BuildUoahsArtIndex()
    {
        return new byte[UoahsArtIdxSize];
    }

    /// <summary>Writes a new-format land record (64-bit flags, textureId, name) for tile <paramref name="id"/>.</summary>
    public static void SetLandNew(byte[] tileData, int id, ulong flags, ushort textureId, string name)
    {
        int group = id / 32;
        int inGroup = id % 32;
        int offset = (group * NewLandGroupSize) + 4 + (inGroup * NewLandRecordSize);

        BinaryPrimitives.WriteUInt64LittleEndian(tileData.AsSpan(offset), flags);
        BinaryPrimitives.WriteUInt16LittleEndian(tileData.AsSpan(offset + 8), textureId);
        WriteName(tileData, offset + 10, name);
    }

    /// <summary>Writes a new-format item record (64-bit flags, height, name) for item <paramref name="id"/> (first group only).</summary>
    public static void SetItemNew(byte[] tileData, int id, ulong flags, byte height, string name)
    {
        int offset = (512 * NewLandGroupSize) + 4 + (id * NewItemRecordSize);

        BinaryPrimitives.WriteUInt64LittleEndian(tileData.AsSpan(offset), flags);
        tileData[offset + 20] = height;
        WriteName(tileData, offset + 21, name);
    }

    /// <summary>Writes an old-format land record (flags, textureId, name) for tile <paramref name="id"/>.</summary>
    public static void SetLand(byte[] tileData, int id, uint flags, ushort textureId, string name)
    {
        int group = id / 32;
        int inGroup = id % 32;
        int offset = (group * LandGroupSize) + 4 + (inGroup * OldLandRecordSize);

        BinaryPrimitives.WriteUInt32LittleEndian(tileData.AsSpan(offset), flags);
        BinaryPrimitives.WriteUInt16LittleEndian(tileData.AsSpan(offset + 4), textureId);
        WriteName(tileData, offset + 6, name);
    }

    /// <summary>Writes an old-format item record (flags, height, name) for item <paramref name="id"/> (first group only).</summary>
    public static void SetItem(byte[] tileData, int id, uint flags, byte height, string name)
    {
        int offset = (512 * LandGroupSize) + 4 + (id * OldItemRecordSize);

        BinaryPrimitives.WriteUInt32LittleEndian(tileData.AsSpan(offset), flags);
        tileData[offset + 16] = height;
        WriteName(tileData, offset + 17, name);
    }

    /// <summary>
    /// Builds a single-block (8x8) map0.mul where every cell has the given land id and z.
    /// </summary>
    public static byte[] BuildMapBlock(ushort landId, sbyte z)
    {
        var buffer = new byte[MapBlockSize];
        for (int cell = 0; cell < 64; cell++)
        {
            int offset = 4 + (cell * 3);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), landId);
            buffer[offset + 2] = unchecked((byte)z);
        }

        return buffer;
    }

    /// <summary>Overwrites one cell of a single-block map built by <see cref="BuildMapBlock"/>.</summary>
    public static void SetMapCell(byte[] mapBlock, int x, int y, ushort landId, sbyte z)
    {
        int offset = 4 + ((((y & 0x7) << 3) + (x & 0x7)) * 3);
        BinaryPrimitives.WriteUInt16LittleEndian(mapBlock.AsSpan(offset), landId);
        mapBlock[offset + 2] = unchecked((byte)z);
    }

    /// <summary>
    /// Builds staidx0.mul + statics0.mul for a single block containing the given statics.
    /// Each static: (id, cellX, cellY, z, hue) with cell coordinates inside the block (0-7).
    /// </summary>
    public static (byte[] Index, byte[] Statics) BuildStatics(
        params (ushort Id, byte CellX, byte CellY, sbyte Z, ushort Hue)[] statics)
    {
        var data = new byte[statics.Length * 7];
        for (int i = 0; i < statics.Length; i++)
        {
            int offset = i * 7;
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
    /// Builds a hues.mul with a single 708-byte block (8 hues). The first hue gets the
    /// provided name, colors, and table range; the rest stay zeroed.
    /// </summary>
    public static byte[] BuildHues(string firstHueName, ushort firstColor, ushort tableStart, ushort tableEnd)
    {
        var buffer = new byte[708];

        int offset = 4;
        for (int c = 0; c < 32; c++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset + (c * 2)), firstColor);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset + 64), tableStart);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset + 66), tableEnd);
        WriteName(buffer, offset + 68, firstHueName);

        return buffer;
    }

    private static void WriteName(byte[] buffer, int offset, string name)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(name);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, 20));
    }
}
