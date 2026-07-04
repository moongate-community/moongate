// /***************************************************************************
//  *
//  * $Author: Turley
//  *
//  * "THE BEER-WARE LICENSE"
//  * As long as you retain this notice you can do whatever you want with
//  * this stuff. If we meet some day, and you think this stuff is worth it,
//  * you can buy me a beer in return.
//  *
//  ***************************************************************************/

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Helpers;
using Moongate.Ultima.Imaging;
using SixLabors.ImageSharp;

namespace Moongate.Ultima.Multi;

public sealed class MultiComponentList
{
    private readonly Point _min;
    private readonly Point _max;

    private Point _center;

    public static readonly MultiComponentList Empty = new();

    public Point Min => _min;
    public Point Max => _max;
    public Point Center => _center;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public MTile[][][] Tiles { get; private set; }
    public int MaxHeight { get; }
    public MultiTileEntry[] SortedTiles { get; }
    public int Surface { get; private set; }

    public static HashSet<ushort> DynamicItemIds { get; set; }

    public struct MultiTileEntry
    {
        public ushort ItemId;
        public short OffsetX;
        public short OffsetY;
        public short OffsetZ;
        public int Flags;
        public int Unk1;
    }

    public MultiComponentList(BinaryReader reader, int count, bool useNewMultiFormat)
    {
        _min = _max = Point.Empty;

        SortedTiles = new MultiTileEntry[count];

        for (var i = 0; i < count; ++i)
        {
            SortedTiles[i].ItemId = Art.GetLegalItemId(reader.ReadUInt16());
            SortedTiles[i].OffsetX = reader.ReadInt16();
            SortedTiles[i].OffsetY = reader.ReadInt16();
            SortedTiles[i].OffsetZ = reader.ReadInt16();
            SortedTiles[i].Flags = reader.ReadInt32();
            SortedTiles[i].Unk1 = useNewMultiFormat ? reader.ReadInt32() : 0;

            var e = SortedTiles[i];

            if (e.OffsetX < _min.X)
            {
                _min.X = e.OffsetX;
            }

            if (e.OffsetY < _min.Y)
            {
                _min.Y = e.OffsetY;
            }

            if (e.OffsetX > _max.X)
            {
                _max.X = e.OffsetX;
            }

            if (e.OffsetY > _max.Y)
            {
                _max.Y = e.OffsetY;
            }

            if (e.OffsetZ > MaxHeight)
            {
                MaxHeight = e.OffsetZ;
            }
        }
        ConvertList();
    }

    public MultiComponentList(string fileName, Multis.ImportType type)
    {
        _min = _max = Point.Empty;

        int itemCount;

        switch (type)
        {
            case Multis.ImportType.TXT:
                {
                    itemCount = 0;

                    using (var ip = new StreamReader(fileName))
                    {
                        while (ip.ReadLine() != null)
                        {
                            itemCount++;
                        }
                    }
                    SortedTiles = new MultiTileEntry[itemCount];
                    itemCount = 0;
                    _min.X = 10000;
                    _min.Y = 10000;

                    using (var ip = new StreamReader(fileName))
                    {
                        while (ip.ReadLine() is { } line)
                        {
                            var split = line.Split(' ');

                            var tmp = split[0];
                            tmp = tmp.Replace("0x", "");

                            SortedTiles[itemCount].ItemId = ushort.Parse(tmp, NumberStyles.HexNumber);
                            SortedTiles[itemCount].OffsetX = Convert.ToInt16(split[1]);
                            SortedTiles[itemCount].OffsetY = Convert.ToInt16(split[2]);
                            SortedTiles[itemCount].OffsetZ = Convert.ToInt16(split[3]);
                            SortedTiles[itemCount].Flags = Convert.ToInt32(split[4]);
                            SortedTiles[itemCount].Unk1 = 0;

                            var e = SortedTiles[itemCount];

                            if (e.OffsetX < _min.X)
                            {
                                _min.X = e.OffsetX;
                            }

                            if (e.OffsetY < _min.Y)
                            {
                                _min.Y = e.OffsetY;
                            }

                            if (e.OffsetX > _max.X)
                            {
                                _max.X = e.OffsetX;
                            }

                            if (e.OffsetY > _max.Y)
                            {
                                _max.Y = e.OffsetY;
                            }

                            if (e.OffsetZ > MaxHeight)
                            {
                                MaxHeight = e.OffsetZ;
                            }

                            itemCount++;
                        }
                    }

                    break;
                }
            case Multis.ImportType.UOA:
                {
                    itemCount = 0;

                    using (var ip = new StreamReader(fileName))
                    {
                        while (ip.ReadLine() is { } line)
                        {
                            ++itemCount;

                            if (itemCount != 4)
                            {
                                continue;
                            }

                            var split = line.Split(' ');
                            itemCount = Convert.ToInt32(split[0]);

                            break;
                        }
                    }
                    SortedTiles = new MultiTileEntry[itemCount];
                    itemCount = 0;
                    _min.X = 10000;
                    _min.Y = 10000;

                    using (var ip = new StreamReader(fileName))
                    {
                        var i = -1;

                        while (ip.ReadLine() is { } line)
                        {
                            ++i;

                            if (i < 4)
                            {
                                continue;
                            }

                            var split = line.Split(' ');

                            SortedTiles[itemCount].ItemId = Convert.ToUInt16(split[0]);
                            SortedTiles[itemCount].OffsetX = Convert.ToInt16(split[1]);
                            SortedTiles[itemCount].OffsetY = Convert.ToInt16(split[2]);
                            SortedTiles[itemCount].OffsetZ = Convert.ToInt16(split[3]);
                            SortedTiles[itemCount].Flags = Convert.ToInt32(split[4]);
                            SortedTiles[itemCount].Unk1 = 0;

                            var e = SortedTiles[itemCount];

                            if (e.OffsetX < _min.X)
                            {
                                _min.X = e.OffsetX;
                            }

                            if (e.OffsetY < _min.Y)
                            {
                                _min.Y = e.OffsetY;
                            }

                            if (e.OffsetX > _max.X)
                            {
                                _max.X = e.OffsetX;
                            }

                            if (e.OffsetY > _max.Y)
                            {
                                _max.Y = e.OffsetY;
                            }

                            if (e.OffsetZ > MaxHeight)
                            {
                                MaxHeight = e.OffsetZ;
                            }

                            ++itemCount;
                        }
                    }

                    break;
                }
            case Multis.ImportType.UOAB:
                {
                    using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var reader = new BinaryReader(fs))
                        {
                            if (reader.ReadInt16() != 1) // Version check
                            {
                                return;
                            }

                            _ = MultiHelpers.ReadUOAString(reader);
                            _ = MultiHelpers.ReadUOAString(reader); // Category
                            _ = MultiHelpers.ReadUOAString(reader); // Subsection

                            _ = reader.ReadInt32();
                            _ = reader.ReadInt32();
                            _ = reader.ReadInt32();
                            _ = reader.ReadInt32();

                            var count = reader.ReadInt32();
                            itemCount = count;
                            SortedTiles = new MultiTileEntry[itemCount];
                            itemCount = 0;
                            _min.X = 10000;
                            _min.Y = 10000;

                            for (; itemCount < count; ++itemCount)
                            {
                                SortedTiles[itemCount].ItemId = (ushort)reader.ReadInt16();
                                SortedTiles[itemCount].OffsetX = reader.ReadInt16();
                                SortedTiles[itemCount].OffsetY = reader.ReadInt16();
                                SortedTiles[itemCount].OffsetZ = reader.ReadInt16();
                                reader.ReadInt16(); // level
                                SortedTiles[itemCount].Flags = 1;
                                reader.ReadInt16(); // hue
                                SortedTiles[itemCount].Unk1 = 0;

                                var e = SortedTiles[itemCount];

                                if (e.OffsetX < _min.X)
                                {
                                    _min.X = e.OffsetX;
                                }

                                if (e.OffsetY < _min.Y)
                                {
                                    _min.Y = e.OffsetY;
                                }

                                if (e.OffsetX > _max.X)
                                {
                                    _max.X = e.OffsetX;
                                }

                                if (e.OffsetY > _max.Y)
                                {
                                    _max.Y = e.OffsetY;
                                }

                                if (e.OffsetZ > MaxHeight)
                                {
                                    MaxHeight = e.OffsetZ;
                                }
                            }
                        }
                    }

                    break;
                }
            case Multis.ImportType.WSC:
                {
                    itemCount = 0;

                    using (var ip = new StreamReader(fileName))
                    {
                        while (ip.ReadLine() is { } line)
                        {
                            line = line.Trim();

                            if (line.StartsWith("SECTION WORLDITEM"))
                            {
                                ++itemCount;
                            }
                        }
                    }
                    SortedTiles = new MultiTileEntry[itemCount];
                    itemCount = 0;
                    _min.X = 10000;
                    _min.Y = 10000;

                    using (var ip = new StreamReader(fileName))
                    {
                        var tempItem = new MultiTileEntry
                        {
                            ItemId = 0xFFFF,
                            Flags = 1,
                            Unk1 = 0
                        };

                        while (ip.ReadLine() is { } line)
                        {
                            line = line.Trim();

                            if (line.StartsWith("SECTION WORLDITEM"))
                            {
                                if (tempItem.ItemId != 0xFFFF)
                                {
                                    if (DynamicItemIds != null && DynamicItemIds.Contains(tempItem.ItemId))
                                    {
                                        tempItem.Flags = 0;
                                    }

                                    SortedTiles[itemCount] = tempItem;
                                    ++itemCount;
                                }

                                tempItem.ItemId = 0xFFFF;
                                tempItem.Flags = 1;
                            }
                            else if (line.StartsWith("ID"))
                            {
                                line = line.Remove(0, 2);
                                line = line.Trim();
                                tempItem.ItemId = Convert.ToUInt16(line);
                            }
                            else if (line.StartsWith('X'))
                            {
                                line = line.Remove(0, 1);
                                line = line.Trim();
                                tempItem.OffsetX = Convert.ToInt16(line);

                                if (tempItem.OffsetX < _min.X)
                                {
                                    _min.X = tempItem.OffsetX;
                                }

                                if (tempItem.OffsetX > _max.X)
                                {
                                    _max.X = tempItem.OffsetX;
                                }
                            }
                            else if (line.StartsWith('Y'))
                            {
                                line = line.Remove(0, 1);
                                line = line.Trim();
                                tempItem.OffsetY = Convert.ToInt16(line);

                                if (tempItem.OffsetY < _min.Y)
                                {
                                    _min.Y = tempItem.OffsetY;
                                }

                                if (tempItem.OffsetY > _max.Y)
                                {
                                    _max.Y = tempItem.OffsetY;
                                }
                            }
                            else if (line.StartsWith('Z'))
                            {
                                line = line.Remove(0, 1);
                                line = line.Trim();
                                tempItem.OffsetZ = Convert.ToInt16(line);

                                if (tempItem.OffsetZ > MaxHeight)
                                {
                                    MaxHeight = tempItem.OffsetZ;
                                }
                            }
                        }

                        if (tempItem.ItemId != 0xFFFF)
                        {
                            if (DynamicItemIds?.Contains(tempItem.ItemId) == true)
                            {
                                tempItem.Flags = 0;
                            }

                            SortedTiles[itemCount] = tempItem;
                        }
                    }

                    // WSC files from a live UO world use absolute world coordinates.
                    // Tool-generated WSC files may already have relative offsets (possibly negative).
                    // Heuristic: if both min coords are positive AND each exceeds the multi's own
                    // extent, the file contains world coordinates and must be normalized.
                    var extentX = _max.X - _min.X;
                    var extentY = _max.Y - _min.Y;

                    if (_min.X > 0 && _min.Y > 0 && _min.X > extentX && _min.Y > extentY)
                    {
                        for (var i = 0; i < SortedTiles.Length; ++i)
                        {
                            SortedTiles[i].OffsetX = (short)(SortedTiles[i].OffsetX - _min.X);
                            SortedTiles[i].OffsetY = (short)(SortedTiles[i].OffsetY - _min.Y);
                        }
                        _max.X -= _min.X;
                        _max.Y -= _min.Y;
                        _min.X = 0;
                        _min.Y = 0;
                    }

                    break;
                }
            case Multis.ImportType.CSV:
                {
                    const string headerCheck = "TileID,OffsetX";

                    itemCount = 0;

                    using (var ip = new StreamReader(fileName))
                    {
                        while (ip.ReadLine() is { } line)
                        {
                            line = line.Trim();

                            if (!line.StartsWith(headerCheck))
                            {
                                ++itemCount;
                            }
                        }
                    }

                    SortedTiles = new MultiTileEntry[itemCount];

                    itemCount = 0;

                    _min.X = 10000;
                    _min.Y = 10000;

                    using (var ip = new StreamReader(fileName))
                    {
                        while (ip.ReadLine() is { } line)
                        {
                            if (line.StartsWith(headerCheck))
                            {
                                continue;
                            }

                            var split = line.Split(',');

                            var tmp = split[0];
                            tmp = tmp.Replace("0x", "");

                            SortedTiles[itemCount].ItemId = ushort.Parse(tmp, NumberStyles.HexNumber);
                            SortedTiles[itemCount].OffsetX = Convert.ToInt16(split[1]);
                            SortedTiles[itemCount].OffsetY = Convert.ToInt16(split[2]);
                            SortedTiles[itemCount].OffsetZ = Convert.ToInt16(split[3]);

                            tmp = split[4];
                            tmp = tmp.Replace("0x", "");

                            SortedTiles[itemCount].Flags = int.Parse(tmp, NumberStyles.HexNumber);
                            SortedTiles[itemCount].Unk1 = 0;

                            var e = SortedTiles[itemCount];

                            if (e.OffsetX < _min.X)
                            {
                                _min.X = e.OffsetX;
                            }

                            if (e.OffsetY < _min.Y)
                            {
                                _min.Y = e.OffsetY;
                            }

                            if (e.OffsetX > _max.X)
                            {
                                _max.X = e.OffsetX;
                            }

                            if (e.OffsetY > _max.Y)
                            {
                                _max.Y = e.OffsetY;
                            }

                            if (e.OffsetZ > MaxHeight)
                            {
                                MaxHeight = e.OffsetZ;
                            }

                            itemCount++;
                        }
                    }

                    break;
                }
        }

        ConvertList();
    }

    public MultiComponentList(List<MultiTileEntry> arr)
    {
        _min = _max = Point.Empty;
        var itemCount = arr.Count;
        SortedTiles = new MultiTileEntry[itemCount];
        _min.X = 10000;
        _min.Y = 10000;
        var i = 0;

        foreach (var entry in arr)
        {
            if (entry.OffsetX < _min.X)
            {
                _min.X = entry.OffsetX;
            }

            if (entry.OffsetY < _min.Y)
            {
                _min.Y = entry.OffsetY;
            }

            if (entry.OffsetX > _max.X)
            {
                _max.X = entry.OffsetX;
            }

            if (entry.OffsetY > _max.Y)
            {
                _max.Y = entry.OffsetY;
            }

            if (entry.OffsetZ > MaxHeight)
            {
                MaxHeight = entry.OffsetZ;
            }

            SortedTiles[i] = entry;

            ++i;
        }
        arr.Clear();

        ConvertList();
    }

    public MultiComponentList(StreamReader stream, int count)
    {
        var itemCount = 0;
        _min = _max = Point.Empty;
        SortedTiles = new MultiTileEntry[count];
        _min.X = 10000;
        _min.Y = 10000;

        while (stream.ReadLine() is { } line)
        {
            var split = Regex.Split(line, @"\s+");
            SortedTiles[itemCount].ItemId = Convert.ToUInt16(split[0]);
            SortedTiles[itemCount].Flags = Convert.ToInt32(split[1]);
            SortedTiles[itemCount].OffsetX = Convert.ToInt16(split[2]);
            SortedTiles[itemCount].OffsetY = Convert.ToInt16(split[3]);
            SortedTiles[itemCount].OffsetZ = Convert.ToInt16(split[4]);
            SortedTiles[itemCount].Unk1 = 0;

            var e = SortedTiles[itemCount];

            if (e.OffsetX < _min.X)
            {
                _min.X = e.OffsetX;
            }

            if (e.OffsetY < _min.Y)
            {
                _min.Y = e.OffsetY;
            }

            if (e.OffsetX > _max.X)
            {
                _max.X = e.OffsetX;
            }

            if (e.OffsetY > _max.Y)
            {
                _max.Y = e.OffsetY;
            }

            if (e.OffsetZ > MaxHeight)
            {
                MaxHeight = e.OffsetZ;
            }

            ++itemCount;

            if (itemCount == count)
            {
                break;
            }
        }

        ConvertList();
    }

    public MultiComponentList(MTileList[][] newTiles, int count, int width, int height)
    {
        _min = _max = Point.Empty;
        SortedTiles = new MultiTileEntry[count];
        _center = new((int)Math.Round(width / 2.0) - 1, (int)Math.Round(height / 2.0) - 1);

        if (_center.X < 0)
        {
            _center.X = width / 2;
        }

        if (_center.Y < 0)
        {
            _center.Y = height / 2;
        }

        MaxHeight = -128;

        var counter = 0;

        for (var x = 0; x < width; ++x)
        {
            for (var y = 0; y < height; ++y)
            {
                foreach (var mTile in newTiles[x][y].ToArray())
                {
                    SortedTiles[counter].ItemId = mTile.Id;
                    SortedTiles[counter].OffsetX = (short)(x - _center.X);
                    SortedTiles[counter].OffsetY = (short)(y - _center.Y);
                    SortedTiles[counter].OffsetZ = mTile.Z;
                    SortedTiles[counter].Flags = mTile.Flag;
                    SortedTiles[counter].Unk1 = 0;

                    if (SortedTiles[counter].OffsetX < _min.X)
                    {
                        _min.X = SortedTiles[counter].OffsetX;
                    }

                    if (SortedTiles[counter].OffsetX > _max.X)
                    {
                        _max.X = SortedTiles[counter].OffsetX;
                    }

                    if (SortedTiles[counter].OffsetY < _min.Y)
                    {
                        _min.Y = SortedTiles[counter].OffsetY;
                    }

                    if (SortedTiles[counter].OffsetY > _max.Y)
                    {
                        _max.Y = SortedTiles[counter].OffsetY;
                    }

                    if (SortedTiles[counter].OffsetZ > MaxHeight)
                    {
                        MaxHeight = SortedTiles[counter].OffsetZ;
                    }

                    ++counter;
                }
            }
        }
        ConvertList();
    }

    private MultiComponentList()
    {
        Tiles = Array.Empty<MTile[][]>();
    }

    /// <summary>
    /// Punt's multi tool csv format
    /// </summary>
    /// <param name="fileName"></param>
    public void ExportToCsvFile(string fileName)
    {
        using (var tex = new StreamWriter(
                   new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite),
                   Encoding.GetEncoding(1252)
               ))
        {
            tex.WriteLine("TileID,OffsetX,OffsetY,OffsetZ,Flag,Cliloc");

            for (var i = 0; i < SortedTiles.Length; ++i)
            {
                tex.WriteLine(
                    $"0x{SortedTiles[i].ItemId:x4},{SortedTiles[i].OffsetX},{SortedTiles[i].OffsetY},{SortedTiles[i].OffsetZ},0x{SortedTiles[i].Flags:x},"
                );
            }
        }
    }

    public void ExportToTextFile(string fileName)
    {
        using (var tex = new StreamWriter(
                   new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite),
                   Encoding.GetEncoding(1252)
               ))
        {
            for (var i = 0; i < SortedTiles.Length; ++i)
            {
                tex.WriteLine(
                    $"0x{SortedTiles[i].ItemId:X} {SortedTiles[i].OffsetX} {SortedTiles[i].OffsetY} {SortedTiles[i].OffsetZ} {SortedTiles[i].Flags}"
                );
            }
        }
    }

    public void ExportToUOAFile(string fileName)
    {
        using (var tex = new StreamWriter(
                   new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite),
                   Encoding.GetEncoding(1252)
               ))
        {
            tex.WriteLine("6 version");
            tex.WriteLine("1 template id");
            tex.WriteLine("-1 item version");
            tex.WriteLine($"{SortedTiles.Length} num components");

            for (var i = 0; i < SortedTiles.Length; ++i)
            {
                tex.WriteLine(
                    $"{SortedTiles[i].ItemId} {SortedTiles[i].OffsetX} {SortedTiles[i].OffsetY} {SortedTiles[i].OffsetZ} {SortedTiles[i].Flags}"
                );
            }
        }
    }

    public void ExportToUox3File(string fileName)
    {
        using (var tex = new StreamWriter(
                   new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite),
                   Encoding.GetEncoding(1252)
               ))
        {
            for (var i = 0; i < SortedTiles.Length; ++i)
            {
                tex.WriteLine($"[HOUSE ITEM {i}]");
                tex.WriteLine("{");
                tex.WriteLine($"ITEM=0x{SortedTiles[i].ItemId:X4}");
                tex.WriteLine($"X={SortedTiles[i].OffsetX}");
                tex.WriteLine($"Y={SortedTiles[i].OffsetY}");
                tex.WriteLine($"Z={SortedTiles[i].OffsetZ}");
                tex.WriteLine("}");
                tex.WriteLine(string.Empty);
            }
        }
    }

    public void ExportToWscFile(string fileName)
    {
        using (var tex = new StreamWriter(
                   new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite),
                   Encoding.GetEncoding(1252)
               ))
        {
            for (var i = 0; i < SortedTiles.Length; ++i)
            {
                tex.WriteLine($"SECTION WORLDITEM {i}");
                tex.WriteLine("{");
                tex.WriteLine($"\tID\t{SortedTiles[i].ItemId}");
                tex.WriteLine($"\tX\t{SortedTiles[i].OffsetX}");
                tex.WriteLine($"\tY\t{SortedTiles[i].OffsetY}");
                tex.WriteLine($"\tZ\t{SortedTiles[i].OffsetZ}");
                tex.WriteLine("\tColor\t0");
                tex.WriteLine("}");
            }
        }
    }

    public void ExportToXmlFile(string fileName, string entryId)
    {
        using (var xmlWriter = XmlWriter.Create(fileName, new() { Indent = true }))
        {
            xmlWriter.WriteStartElement("Entry");
            xmlWriter.WriteAttributeString("ID", entryId);

            for (var i = 0; i < SortedTiles.Length; ++i)
            {
                xmlWriter.WriteStartElement("Item");
                xmlWriter.WriteAttributeString("X", SortedTiles[i].OffsetX.ToString());
                xmlWriter.WriteAttributeString("Y", SortedTiles[i].OffsetY.ToString());
                xmlWriter.WriteAttributeString("Z", SortedTiles[i].OffsetZ.ToString());
                xmlWriter.WriteAttributeString("ID", $"0x{SortedTiles[i].ItemId:X4}");
                xmlWriter.WriteEndElement(); // Item
            }

            xmlWriter.WriteEndElement();
        }
    }

    /// <summary>
    /// Returns Bitmap of Multi to maximumHeight
    /// </summary>
    /// <param name="maximumHeight"></param>
    /// <returns></returns>
    public UltimaBitmap GetImage(int maximumHeight = 300)
    {
        if (Width == 0 || Height == 0)
        {
            return null;
        }

        int xMin = 1000,
            yMin = 1000;
        int xMax = -1000,
            yMax = -1000;

        for (var x = 0; x < Width; ++x)
        {
            for (var y = 0; y < Height; ++y)
            {
                foreach (var mTile in Tiles[x][y])
                {
                    var bmp = Art.GetStatic(mTile.Id);

                    if (bmp == null)
                    {
                        continue;
                    }

                    var px = (x - y) * 22;
                    var py = (x + y) * 22;

                    px -= bmp.Width / 2;
                    py -= mTile.Z << 2;
                    py -= bmp.Height;

                    if (px < xMin)
                    {
                        xMin = px;
                    }

                    if (py < yMin)
                    {
                        yMin = py;
                    }

                    px += bmp.Width;
                    py += bmp.Height;

                    if (px > xMax)
                    {
                        xMax = px;
                    }

                    if (py > yMax)
                    {
                        yMax = py;
                    }
                }
            }
        }

        var canvas = new UltimaBitmap(xMax - xMin, yMax - yMin);

        for (var x = 0; x < Width; ++x)
        {
            for (var y = 0; y < Height; ++y)
            {
                foreach (var mTile in Tiles[x][y])
                {
                    var bmp = Art.GetStatic(mTile.Id);

                    if (bmp == null)
                    {
                        continue;
                    }

                    if (mTile.Z > maximumHeight)
                    {
                        continue;
                    }

                    var px = (x - y) * 22;
                    var py = (x + y) * 22;

                    px -= bmp.Width / 2;
                    py -= mTile.Z << 2;
                    py -= bmp.Height;
                    px -= xMin;
                    py -= yMin;

                    bmp.DrawInto(canvas, px, py);
                }
            }
        }

        return canvas;
    }

    private void ConvertList()
    {
        _center = new(-_min.X, -_min.Y);
        Width = _max.X - _min.X + 1;
        Height = _max.Y - _min.Y + 1;

        var tiles = new MTileList[Width][];
        Tiles = new MTile[Width][][];

        for (var x = 0; x < Width; ++x)
        {
            tiles[x] = new MTileList[Height];
            Tiles[x] = new MTile[Height][];

            for (var y = 0; y < Height; ++y)
            {
                tiles[x][y] = new();
            }
        }

        for (var i = 0; i < SortedTiles.Length; ++i)
        {
            var xOffset = SortedTiles[i].OffsetX + _center.X;
            var yOffset = SortedTiles[i].OffsetY + _center.Y;

            tiles[xOffset][yOffset]
                .Add(
                    SortedTiles[i].ItemId,
                    (sbyte)SortedTiles[i].OffsetZ,
                    (sbyte)SortedTiles[i].Flags,
                    SortedTiles[i].Unk1
                );
        }

        Surface = 0;

        for (var x = 0; x < Width; ++x)
        {
            for (var y = 0; y < Height; ++y)
            {
                Tiles[x][y] = tiles[x][y].ToArray();

                for (var i = 0; i < Tiles[x][y].Length; ++i)
                {
                    Tiles[x][y][i].Solver = i;
                }

                if (Tiles[x][y].Length > 1)
                {
                    Array.Sort(Tiles[x][y]);
                }

                if (Tiles[x][y].Length > 0)
                {
                    ++Surface;
                }
            }
        }
    }
}
