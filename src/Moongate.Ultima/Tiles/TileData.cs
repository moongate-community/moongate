using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Moongate.Ultima.Helpers;

using Moongate.Ultima.Types;

using Moongate.Ultima.Io;

using Moongate.Ultima.Graphics;

namespace Moongate.Ultima.Tiles;

// TODO: move import/export csv routines to separate class

/// <summary>
/// Contains lists of <see cref="LandData">land</see> and <see cref="ItemData">item</see> tile data.
/// <seealso cref="LandData" />
/// <seealso cref="ItemData" />
/// </summary>
public static class TileData
{
    /// <summary>
    /// Gets the list of <see cref="LandData">land tile data</see>.
    /// </summary>
    public static LandData[] LandTable { get; private set; }

    /// <summary>
    /// Gets the list of <see cref="ItemData">item tile data</see>.
    /// </summary>
    public static ItemData[] ItemTable { get; private set; }

    public static int[] HeightTable { get; private set; }

    private static int[] _landHeader;
    private static int[] _itemHeader;

    static TileData()
    {
        Initialize();
    }

    public static unsafe void Initialize()
    {
        string filePath = Files.GetFilePath("tiledata.mul");
        if (filePath == null)
        {
            return;
        }

        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            bool useNeWTileDataFormat = Art.IsUOAHS();
            _landHeader = new int[512];
            int j = 0;
            LandTable = new LandData[0x4000];

            var buffer = new byte[fs.Length];
            fs.ReadExactly(buffer, 0, buffer.Length);
            int currentPos = 0;

            int landStructSize = useNeWTileDataFormat ? sizeof(NewLandTileDataMul) : sizeof(OldLandTileDataMul);

            for (int i = 0; i < 0x4000; i += 32)
            {
                _landHeader[j++] = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(currentPos));
                currentPos += 4;
                for (int count = 0; count < 32; ++count)
                {
                    if (useNeWTileDataFormat)
                    {
                        var cur = Unsafe.ReadUnaligned<NewLandTileDataMul>(ref buffer[currentPos]);
                        LandTable[i + count] = new LandData(cur);
                    }
                    else
                    {
                        var cur = Unsafe.ReadUnaligned<OldLandTileDataMul>(ref buffer[currentPos]);
                        LandTable[i + count] = new LandData(cur);
                    }
                    currentPos += landStructSize;
                }
            }

            long remaining = buffer.Length - currentPos;

            int structSize = useNeWTileDataFormat ? sizeof(NewItemTileDataMul) : sizeof(OldItemTileDataMul);

            _itemHeader = new int[remaining / ((structSize * 32) + 4)];
            int itemLength = _itemHeader.Length * 32;

            ItemTable = new ItemData[itemLength];
            HeightTable = new int[itemLength];

            j = 0;
            for (int i = 0; i < itemLength; i += 32)
            {
                _itemHeader[j++] = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(currentPos));
                currentPos += 4;
                for (int count = 0; count < 32; ++count)
                {
                    if (useNeWTileDataFormat)
                    {
                        var cur = Unsafe.ReadUnaligned<NewItemTileDataMul>(ref buffer[currentPos]);
                        ItemTable[i + count] = new ItemData(cur);
                        HeightTable[i + count] = cur.height;
                    }
                    else
                    {
                        var cur = Unsafe.ReadUnaligned<OldItemTileDataMul>(ref buffer[currentPos]);
                        ItemTable[i + count] = new ItemData(cur);
                        HeightTable[i + count] = cur.height;
                    }
                    currentPos += structSize;
                }
            }
        }
    }

    /// <summary>
    /// Saves <see cref="LandData"/> and <see cref="ItemData"/> to tiledata.mul
    /// </summary>
    /// <param name="fileName"></param>
    public static void SaveTileData(string fileName)
    {
        using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Write))
        using (var bin = new BinaryWriter(fs))
        {
            int j = 0;
            bool useNewTileDataFormat = Art.IsUOAHS();
            for (int i = 0; i < 0x4000; ++i)
            {
                if ((i & 0x1F) == 0)
                {
                    bin.Write(_landHeader[j++]); // header
                }

                if (useNewTileDataFormat)
                {
                    bin.Write((ulong)LandTable[i].Flags);
                }
                else
                {
                    bin.Write((uint)LandTable[i].Flags);
                }

                bin.Write(LandTable[i].TextureId);
                var b = new byte[20];
                if (LandTable[i].Name != null)
                {
                    byte[] bb = Encoding.ASCII.GetBytes(LandTable[i].Name);
                    if (bb.Length > 20)
                    {
                        Array.Resize(ref bb, 20);
                    }

                    bb.CopyTo(b, 0);
                }

                bin.Write(b);
            }

            j = 0;

            for (int i = 0; i < ItemTable.Length; ++i)
            {
                if ((i & 0x1F) == 0)
                {
                    bin.Write(_itemHeader[j++]); // header
                }

                if (useNewTileDataFormat)
                {
                    bin.Write((ulong)ItemTable[i].Flags);
                }
                else
                {
                    bin.Write((uint)ItemTable[i].Flags);
                }

                bin.Write(ItemTable[i].Weight);
                bin.Write(ItemTable[i].Quality);
                bin.Write(ItemTable[i].MiscData);
                bin.Write(ItemTable[i].Unk2);
                bin.Write(ItemTable[i].Quantity);
                bin.Write(ItemTable[i].Animation);
                bin.Write(ItemTable[i].Unk3);
                bin.Write(ItemTable[i].Hue);
                bin.Write(ItemTable[i].StackingOffset); // unk4
                bin.Write(ItemTable[i].Value); // unk5
                bin.Write(ItemTable[i].Height);

                var b = new byte[20];
                if (ItemTable[i].Name != null)
                {
                    byte[] bb = Encoding.ASCII.GetBytes(ItemTable[i].Name);
                    if (bb.Length > 20)
                    {
                        Array.Resize(ref bb, 20);
                    }

                    bb.CopyTo(b, 0);
                }

                bin.Write(b);
            }
        }
    }

    /// <summary>
    /// Exports <see cref="ItemData"/> to csv file
    /// </summary>
    /// <param name="fileName"></param>
    public static void ExportItemDataToCsv(string fileName)
    {
        using (var tex = new StreamWriter(new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite), Encoding.GetEncoding(1252)))
        {
            tex.Write("ID;Name;Weight/Quantity;Layer/Quality;Gump/AnimID;Height;Hue;Class/Quantity;StackingOffset;miscData;Unknown2;Unknown3");

            string columnNames = GetTileFlagColumnNames();
            tex.Write($"{columnNames}\r\n");

            for (int i = 0; i < ItemTable.Length; ++i)
            {
                ItemData tile = ItemTable[i];
                tex.Write("0x{0:X4}", i);
                tex.Write(";{0}", tile.Name);
                tex.Write($";{tile.Weight}");
                tex.Write($";{tile.Quality}");
                tex.Write(";0x{0:X4}", tile.Animation);
                tex.Write($";{tile.Height}");
                tex.Write($";{tile.Hue}");
                tex.Write($";{tile.Quantity}");
                tex.Write($";{tile.StackingOffset}");
                tex.Write($";{tile.MiscData}");
                tex.Write($";{tile.Unk2}");
                tex.Write($";{tile.Unk3}");

                TileFlag[] enumValues = Enum.GetValues<TileFlag>();
                int maxLength = Art.IsUOAHS() ? enumValues.Length : (enumValues.Length / 2) + 1;
                for (int t = 1; t < maxLength; ++t)
                {
                    tex.Write($";{((tile.Flags & enumValues[t]) != 0 ? "1" : "0")}");
                }
                tex.Write("\r\n");
            }
        }
    }

    private static string GetTileFlagColumnNames()
    {
        string[] enumNames = Enum.GetNames<TileFlag>();
        int maxLength = Art.IsUOAHS() ? enumNames.Length : (enumNames.Length / 2) + 1;

        // full column string length for latest client is around ~580 characters
        const int capacity = 600;

        var stringBuilder = new StringBuilder(capacity);

        for (int i = 1; i < maxLength; ++i)
        {
            stringBuilder.Append(';').Append(enumNames[i]);
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Exports <see cref="LandData"/> to csv file
    /// </summary>
    /// <param name="fileName"></param>
    public static void ExportLandDataToCsv(string fileName)
    {
        using (var tex = new StreamWriter(new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite)))
        {
            tex.Write("ID;Name;TextureId");

            string columnNames = GetTileFlagColumnNames();
            tex.Write($"{columnNames}\r\n");

            for (int i = 0; i < LandTable.Length; ++i)
            {
                LandData tile = LandTable[i];
                tex.Write("0x{0:X4}", i);
                tex.Write($";{tile.Name}");
                tex.Write($";0x{tile.TextureId:X4}");

                TileFlag[] enumValues = Enum.GetValues<TileFlag>();
                int maxLength = Art.IsUOAHS() ? enumValues.Length : (enumValues.Length / 2) + 1;
                for (int t = 1; t < maxLength; ++t)
                {
                    tex.Write($";{((tile.Flags & enumValues[t]) != 0 ? "1" : "0")}");
                }
                tex.Write("\r\n");
            }
        }
    }

    public static void ImportItemDataFromCsv(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return;
        }

        using (var sr = new StreamReader(fileName))
        {
            while (sr.ReadLine() is { } line)
            {
                line = line.Trim();

                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                if (line.StartsWith("ID;"))
                {
                    continue;
                }

                string[] split = line.Split(';');
                if (split.Length < 44)
                {
                    continue;
                }

                int id = TileDataHelpers.ConvertStringToInt(split[0]);
                if (id < 0 || id >= ItemTable.Length)
                {
                    continue;
                }

                try
                {
                    ItemTable[id].ReadData(split);
                }
                catch
                {
                    // Malformed CSV field value — skip
                }
            }
        }
    }

    public static void ImportLandDataFromCsv(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return;
        }

        using (var sr = new StreamReader(fileName))
        {
            while (sr.ReadLine() is { } line)
            {
                line = line.Trim();

                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                if (line.StartsWith("ID;"))
                {
                    continue;
                }

                string[] split = line.Split(';');
                if (split.Length < 35)
                {
                    continue;
                }

                int id = TileDataHelpers.ConvertStringToInt(split[0]);
                if (id < 0 || id >= LandTable.Length)
                {
                    continue;
                }

                try
                {
                    LandTable[id].ReadData(split);
                }
                catch
                {
                    // Malformed CSV field value — skip
                }
            }
        }
    }
}
