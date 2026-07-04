using System.Text;
using System.Text.RegularExpressions;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Helpers;
using Moongate.Ultima.Io;

namespace Moongate.Ultima.Multi;

public sealed class Multis
{
    public const int MaximumMultiIndex = 0x2200;

    private static MultiComponentList[] _components = new MultiComponentList[MaximumMultiIndex];
    private static FileIndex _fileIndex = new("Multi.idx", "Multi.mul", MaximumMultiIndex, 14);

    private static MultiComponentList[] _uopComponents = new MultiComponentList[MaximumMultiIndex];
    private static bool _uopLoaded;

    public enum ImportType
    {
        TXT,
        UOA,
        UOAB,
        WSC,
        CSV, // Punt's multi tool csv format
        UOX3,
        MULTICACHE,
        UOADESIGN,
        XML
    }

    public static bool HasUopFile => !string.IsNullOrEmpty(Files.GetFilePath("multicollection.uop"));

    public static void Add(int index, MultiComponentList comp)
        => _components[index] = comp;

    /// <summary>
    /// Gets <see cref="MultiComponentList" /> of multi
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public static MultiComponentList GetComponents(int index)
    {
        MultiComponentList mcl;

        if (index >= 0 && index < _components.Length)
        {
            mcl = _components[index];

            if (mcl == null)
            {
                _components[index] = mcl = Load(index);
            }
        }
        else
        {
            mcl = MultiComponentList.Empty;
        }

        return mcl;
    }

    public static MultiComponentList GetUopComponents(int index)
    {
        if (!_uopLoaded)
        {
            LoadUop();
        }

        if (index >= 0 && index < _uopComponents.Length)
        {
            return _uopComponents[index] ?? MultiComponentList.Empty;
        }

        return MultiComponentList.Empty;
    }

    public static MultiComponentList ImportFromFile(int index, string fileName, ImportType type)
    {
        try
        {
            return _components[index] = new(fileName, type);
        }
        catch
        {
            return _components[index] = MultiComponentList.Empty;
        }
    }

    public static MultiComponentList Load(int index)
    {
        try
        {
            var stream = _fileIndex.Seek(index, out var length, out var _, out var _);

            if (stream == null)
            {
                return MultiComponentList.Empty;
            }

            // leaveOpen: stream is owned by the shared FileIndex; the
            // BinaryReader is throwaway and must not close it.
            if (Art.IsUOAHS())
            {
                return new(new(stream, Encoding.UTF8, true), length / 16, true);
            }

            return new(new(stream, Encoding.UTF8, true), length / 12, false);
        }
        catch
        {
            return MultiComponentList.Empty;
        }
    }

    public static List<MultiComponentList> LoadFromCache(string fileName)
    {
        var multiComponentLists = new List<MultiComponentList>();

        using (var ip = new StreamReader(fileName))
        {
            while (ip.ReadLine() is { } line)
            {
                var split = Regex.Split(line, @"\s+");

                if (split.Length != 7)
                {
                    continue;
                }

                var count = Convert.ToInt32(split[2]);
                multiComponentLists.Add(new(ip, count));
            }
        }

        return multiComponentLists;
    }

    public static List<object[]> LoadFromDesigner(string fileName)
    {
        var multiList = new List<object[]>();

        var root = Path.GetFileNameWithoutExtension(fileName);
        var idx = $"{root}.idx";
        var bin = $"{root}.bin";

        if (!File.Exists(idx) || !File.Exists(bin))
        {
            return multiList;
        }

        using (var idxfs = new FileStream(idx, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using (var binfs = new FileStream(bin, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var idxbin = new BinaryReader(idxfs))
                {
                    using (var binbin = new BinaryReader(binfs))
                    {
                        var count = idxbin.ReadInt32();
                        var version = idxbin.ReadInt32();

                        for (var i = 0; i < count; ++i)
                        {
                            var data = new object[2];

                            switch (version)
                            {
                                case 0:
                                    data[0] = MultiHelpers.ReadUOAString(idxbin);
                                    var arr = new List<MultiComponentList.MultiTileEntry>();
                                    data[0] += "-" + MultiHelpers.ReadUOAString(idxbin);
                                    data[0] += "-" + MultiHelpers.ReadUOAString(idxbin);

                                    _ = idxbin.ReadInt32();
                                    _ = idxbin.ReadInt32();
                                    _ = idxbin.ReadInt32();
                                    _ = idxbin.ReadInt32();

                                    var filepos = idxbin.ReadInt64();
                                    var reccount = idxbin.ReadInt32();

                                    binbin.BaseStream.Seek(filepos, SeekOrigin.Begin);

                                    for (var j = 0; j < reccount; ++j)
                                    {
                                        int x;
                                        int y;
                                        int z;
                                        var index = x = y = z = 0;

                                        switch (binbin.ReadInt32())
                                        {
                                            case 0:
                                                index = binbin.ReadInt32();
                                                x = binbin.ReadInt32();
                                                y = binbin.ReadInt32();
                                                z = binbin.ReadInt32();
                                                binbin.ReadInt32();

                                                break;

                                            case 1:
                                                index = binbin.ReadInt32();
                                                x = binbin.ReadInt32();
                                                y = binbin.ReadInt32();
                                                z = binbin.ReadInt32();
                                                binbin.ReadInt32();
                                                binbin.ReadInt32();

                                                break;
                                        }

                                        var tempItem =
                                            new MultiComponentList.MultiTileEntry
                                            {
                                                ItemId = (ushort)index,
                                                Flags = 1,
                                                OffsetX = (short)x,
                                                OffsetY = (short)y,
                                                OffsetZ = (short)z,
                                                Unk1 = 0
                                            };
                                        arr.Add(tempItem);
                                    }

                                    data[1] = new MultiComponentList(arr);

                                    break;
                            }

                            multiList.Add(data);
                        }
                    }
                }

                return multiList;
            }
        }
    }

    public static MultiComponentList LoadFromFile(string fileName, ImportType type)
    {
        try
        {
            return new(fileName, type);
        }
        catch
        {
            return MultiComponentList.Empty;
        }
    }

    /// <summary>
    /// ReReads multi.mul
    /// </summary>
    public static void Reload()
    {
        _fileIndex = new("Multi.idx", "Multi.mul", MaximumMultiIndex, 14);
        _components = new MultiComponentList[MaximumMultiIndex];
        ReloadUop();
    }

    public static void ReloadUop()
    {
        _uopComponents = new MultiComponentList[MaximumMultiIndex];
        _uopLoaded = false;
    }

    public static void Remove(int index)
        => _components[index] = MultiComponentList.Empty;

    public static void Save(string path)
    {
        var isUOAHS = Art.IsUOAHS();

        var idx = Path.Combine(path, "multi.idx");
        var mul = Path.Combine(path, "multi.mul");

        using (var fsidx = new FileStream(idx, FileMode.Create, FileAccess.Write, FileShare.Write))
        {
            using (var fsmul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                using (var binidx = new BinaryWriter(fsidx))
                {
                    using (var binmul = new BinaryWriter(fsmul))
                    {
                        for (var index = 0; index < MaximumMultiIndex; ++index)
                        {
                            var comp = GetComponents(index);

                            if (comp == MultiComponentList.Empty)
                            {
                                binidx.Write(-1); // lookup
                                binidx.Write(-1); // length
                                binidx.Write(-1); // extra
                            }
                            else
                            {
                                var tiles = RebuildTiles(comp.SortedTiles);
                                binidx.Write((int)fsmul.Position); // lookup

                                if (isUOAHS)
                                {
                                    binidx.Write(tiles.Count * 16); // length
                                }
                                else
                                {
                                    binidx.Write(tiles.Count * 12); // length
                                }

                                binidx.Write(-1); // extra

                                for (var i = 0; i < tiles.Count; ++i)
                                {
                                    binmul.Write(tiles[i].ItemId);
                                    binmul.Write(tiles[i].OffsetX);
                                    binmul.Write(tiles[i].OffsetY);
                                    binmul.Write(tiles[i].OffsetZ);
                                    binmul.Write(tiles[i].Flags);

                                    if (isUOAHS)
                                    {
                                        binmul.Write(tiles[i].Unk1);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static void LoadUop()
    {
        _uopLoaded = true;

        var path = Files.GetFilePath("multicollection.uop");

        if (path == null)
        {
            return;
        }

        try
        {
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(fileStream);

            var magic = reader.ReadUInt32();

            if (magic != 0x0050594D)
            {
                return;
            }

            var version = reader.ReadUInt32();

            if (version > 5)
            {
                return;
            }

            reader.ReadUInt32(); // signature
            var nextTableOffset = reader.ReadUInt64();
            reader.ReadUInt32(); // block capacity
            reader.ReadUInt32(); // file count
            reader.ReadUInt32(); // reserved
            reader.ReadUInt32(); // reserved
            reader.ReadUInt32(); // reserved

            var entries = new List<(long dataOffset, uint compressedSize, uint decompressedSize)>();

            var next = nextTableOffset;

            while (next != 0)
            {
                fileStream.Seek((long)next, SeekOrigin.Begin);
                var count = reader.ReadInt32();
                next = reader.ReadUInt64();

                for (var i = 0; i < count; i++)
                {
                    var dataOffset = reader.ReadUInt64();
                    var headerSize = reader.ReadUInt32();
                    var compressedSize = reader.ReadUInt32();
                    var decompressedSize = reader.ReadUInt32();
                    reader.ReadUInt64(); // hash
                    reader.ReadUInt32(); // unknown
                    var flag = reader.ReadUInt16();

                    if (dataOffset == 0 || decompressedSize == 0)
                    {
                        continue;
                    }

                    if (flag == 0)
                    {
                        compressedSize = 0;
                    }

                    entries.Add(((long)(dataOffset + headerSize), compressedSize, decompressedSize));
                }
            }

            foreach (var (dataOffset, compressedSize, decompressedSize) in entries)
            {
                fileStream.Seek(dataOffset, SeekOrigin.Begin);

                byte[] raw;

                if (compressedSize > 0)
                {
                    var compressed = reader.ReadBytes((int)compressedSize);
                    var (ok, decompressed) = UopUtils.Decompress(compressed);

                    if (!ok)
                    {
                        continue;
                    }

                    raw = decompressed;
                }
                else
                {
                    raw = reader.ReadBytes((int)decompressedSize);
                }

                using var memoryStream = new MemoryStream(raw);
                using var binaryReader = new BinaryReader(memoryStream);

                var multiId = binaryReader.ReadUInt32();
                var componentCount = binaryReader.ReadInt32();

                if (multiId >= MaximumMultiIndex || componentCount <= 0)
                {
                    continue;
                }

                var tiles = new List<MultiComponentList.MultiTileEntry>(componentCount);

                for (var j = 0; j < componentCount; j++)
                {
                    var graphic = binaryReader.ReadUInt16();
                    var ux = binaryReader.ReadUInt16();
                    var uy = binaryReader.ReadUInt16();
                    var uz = binaryReader.ReadUInt16();
                    var uflags = binaryReader.ReadUInt16();
                    var clilocsCount = binaryReader.ReadInt32();

                    if (clilocsCount > 0)
                    {
                        binaryReader.BaseStream.Seek(clilocsCount * 4L, SeekOrigin.Current);
                    }

                    tiles.Add(
                        new()
                        {
                            ItemId = graphic,
                            OffsetX = (short)ux,
                            OffsetY = (short)uy,
                            OffsetZ = (short)uz,
                            Flags = uflags != 0 ? 0 : 1,
                            Unk1 = 0
                        }
                    );
                }

                if (tiles.Count > 0)
                {
                    _uopComponents[multiId] = new(tiles);
                }
            }
        }
        catch
        {
            // leave array in its current partially-populated state
        }
    }

    private static List<MultiComponentList.MultiTileEntry> RebuildTiles(MultiComponentList.MultiTileEntry[] tiles)
    {
        var newTiles = new List<MultiComponentList.MultiTileEntry>();
        newTiles.AddRange(tiles);

        if (newTiles[0].OffsetX == 0 && newTiles[0].OffsetY == 0 && newTiles[0].OffsetZ == 0) // found a center item
        {
            if (newTiles[0].ItemId != 0x1) // its a "good" one
            {
                for (var j = newTiles.Count - 1; j >= 0; --j) // remove all invis items
                {
                    if (newTiles[j].ItemId == 0x1)
                    {
                        newTiles.RemoveAt(j);
                    }
                }

                return newTiles;
            }

            // a bad one
            for (var i = 1; i < newTiles.Count; ++i) // do we have a better one?
            {
                if (newTiles[i].OffsetX != 0 ||
                    newTiles[i].OffsetY != 0 ||
                    newTiles[i].ItemId == 0x1 ||
                    newTiles[i].OffsetZ != 0)
                {
                    continue;
                }

                var centerItem = newTiles[i];
                newTiles.RemoveAt(i); // jep so save it

                for (var j = newTiles.Count - 1; j >= 0; --j) // and remove all invis
                {
                    if (newTiles[j].ItemId == 0x1)
                    {
                        newTiles.RemoveAt(j);
                    }
                }

                newTiles.Insert(0, centerItem);

                return newTiles;
            }

            for (var j = newTiles.Count - 1; j >= 1; --j) // nothing found so remove all invis except the first
            {
                if (newTiles[j].ItemId == 0x1)
                {
                    newTiles.RemoveAt(j);
                }
            }

            return newTiles;
        }

        for (var i = 0; i < newTiles.Count; ++i) // is there a good one
        {
            if (newTiles[i].OffsetX != 0 ||
                newTiles[i].OffsetY != 0 ||
                newTiles[i].ItemId == 0x1 ||
                newTiles[i].OffsetZ != 0)
            {
                continue;
            }

            var centerItem = newTiles[i];
            newTiles.RemoveAt(i); // store it

            for (var j = newTiles.Count - 1; j >= 0; --j) // remove all invis
            {
                if (newTiles[j].ItemId == 0x1)
                {
                    newTiles.RemoveAt(j);
                }
            }

            newTiles.Insert(0, centerItem);

            return newTiles;
        }

        for (var j = newTiles.Count - 1; j >= 0; --j) // nothing found so remove all invis
        {
            if (newTiles[j].ItemId == 0x1)
            {
                newTiles.RemoveAt(j);
            }
        }

        // and create a new invis
        var invisItem =
            new MultiComponentList.MultiTileEntry
            {
                ItemId = 0x1,
                OffsetX = 0,
                OffsetY = 0,
                OffsetZ = 0,
                Flags = 0,
                Unk1 = 0
            };

        newTiles.Insert(0, invisItem);

        return newTiles;
    }
}
