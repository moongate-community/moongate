using System.Runtime.InteropServices;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Io;

namespace Moongate.Ultima.Maps;

public sealed class TileMatrixPatch
{
    public int LandBlocksCount { get; }
    public int StaticBlocksCount { get; }
    public Tile[][][] LandBlocks { get; }
    public HuedTile[][][][][] StaticBlocks { get; }

    private readonly int _blockWidth;
    private readonly int _blockHeight;

    private static StaticTile[] _tileBuffer = new StaticTile[128];

    public TileMatrixPatch(TileMatrix matrix, int index, string path)
    {
        _blockWidth = matrix.BlockWidth;
        _blockHeight = matrix.BlockWidth;

        LandBlocksCount = StaticBlocksCount = 0;
        string mapDataPath,
               mapIndexPath;

        if (path == null)
        {
            mapDataPath = Files.GetFilePath($"mapdif{index}.mul");
            mapIndexPath = Files.GetFilePath($"mapdifl{index}.mul");
        }
        else
        {
            mapDataPath = Path.Combine(path, $"mapdif{index}.mul");

            if (!File.Exists(mapDataPath))
            {
                mapDataPath = null;
            }

            mapIndexPath = Path.Combine(path, $"mapdifl{index}.mul");

            if (!File.Exists(mapIndexPath))
            {
                mapIndexPath = null;
            }
        }

        if (mapDataPath != null && mapIndexPath != null)
        {
            LandBlocks = new Tile[matrix.BlockWidth][][];
            LandBlocksCount = PatchLand(matrix, mapDataPath, mapIndexPath);
        }

        string staDataPath,
               staIndexPath,
               staLookupPath;

        if (path == null)
        {
            staDataPath = Files.GetFilePath($"stadif{index}.mul");
            staIndexPath = Files.GetFilePath($"stadifl{index}.mul");
            staLookupPath = Files.GetFilePath($"stadifi{index}.mul");
        }
        else
        {
            staDataPath = Path.Combine(path, $"stadif{index}.mul");

            if (!File.Exists(staDataPath))
            {
                staDataPath = null;
            }

            staIndexPath = Path.Combine(path, $"stadifl{index}.mul");

            if (!File.Exists(staIndexPath))
            {
                staIndexPath = null;
            }

            staLookupPath = Path.Combine(path, $"stadifi{index}.mul");

            if (!File.Exists(staLookupPath))
            {
                staLookupPath = null;
            }
        }

        if (staDataPath == null || staIndexPath == null || staLookupPath == null)
        {
            return;
        }

        StaticBlocks = new HuedTile[matrix.BlockWidth][][][][];
        StaticBlocksCount = PatchStatics(matrix, staDataPath, staIndexPath, staLookupPath);
    }

    public Tile[] GetLandBlock(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _blockWidth || y >= _blockHeight)
        {
            return TileMatrix.InvalidLandBlock;
        }

        if (LandBlocks[x] == null)
        {
            return TileMatrix.InvalidLandBlock;
        }

        return LandBlocks[x][y];
    }

    public Tile GetLandTile(int x, int y)
        => GetLandBlock(x >> 3, y >> 3)[((y & 0x7) << 3) + (x & 0x7)];

    public HuedTile[][][] GetStaticBlock(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _blockWidth || y >= _blockHeight)
        {
            return TileMatrix.EmptyStaticBlock;
        }

        if (StaticBlocks[x] == null)
        {
            return TileMatrix.EmptyStaticBlock;
        }

        return StaticBlocks[x][y];
    }

    public HuedTile[] GetStaticTiles(int x, int y)
        => GetStaticBlock(x >> 3, y >> 3)[x & 0x7][y & 0x7];

    public bool IsLandBlockPatched(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _blockWidth || y >= _blockHeight)
        {
            return false;
        }

        if (LandBlocks[x] == null)
        {
            return false;
        }

        if (LandBlocks[x][y] == null)
        {
            return false;
        }

        return true;
    }

    public bool IsStaticBlockPatched(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _blockWidth || y >= _blockHeight)
        {
            return false;
        }

        if (StaticBlocks[x] == null)
        {
            return false;
        }

        if (StaticBlocks[x][y] == null)
        {
            return false;
        }

        return true;
    }

    private int PatchLand(TileMatrix matrix, string dataPath, string indexPath)
    {
        using (var fsData = new FileStream(dataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using (var fsIndex = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var indexReader = new BinaryReader(fsIndex))
                {
                    var count = (int)(indexReader.BaseStream.Length / 4);

                    for (var i = 0; i < count; ++i)
                    {
                        var blockId = indexReader.ReadInt32();
                        var x = blockId / matrix.BlockHeight;
                        var y = blockId % matrix.BlockHeight;

                        fsData.Seek(4, SeekOrigin.Current);

                        var tiles = new Tile[64];

                        fsData.ReadExactly(MemoryMarshal.AsBytes(tiles.AsSpan()));

                        if (LandBlocks[x] == null)
                        {
                            LandBlocks[x] = new Tile[matrix.BlockHeight][];
                        }

                        LandBlocks[x][y] = tiles;
                    }

                    return count;
                }
            }
        }
    }

    private int PatchStatics(TileMatrix matrix, string dataPath, string indexPath, string lookupPath)
    {
        using (var fsData = new FileStream(dataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using (var fsIndex = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var fsLookup = new FileStream(lookupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (BinaryReader indexReader = new(fsIndex),
                                        lookupReader = new(fsLookup))
                    {
                        var count = Math.Min(
                            (int)(indexReader.BaseStream.Length / 4),
                            (int)(lookupReader.BaseStream.Length / 12)
                        );

                        var lists = new HuedTileList[8][];

                        for (var x = 0; x < 8; ++x)
                        {
                            lists[x] = new HuedTileList[8];

                            for (var y = 0; y < 8; ++y)
                            {
                                lists[x][y] = new();
                            }
                        }

                        for (var i = 0; i < count; ++i)
                        {
                            var blockId = indexReader.ReadInt32();
                            var blockX = blockId / matrix.BlockHeight;
                            var blockY = blockId % matrix.BlockHeight;

                            var offset = lookupReader.ReadInt32();
                            var length = lookupReader.ReadInt32();

                            lookupReader.ReadInt32(); // Extra

                            if (offset < 0 || length <= 0)
                            {
                                if (StaticBlocks[blockX] == null)
                                {
                                    StaticBlocks[blockX] = new HuedTile[matrix.BlockHeight][][][];
                                }

                                StaticBlocks[blockX][blockY] = TileMatrix.EmptyStaticBlock;

                                continue;
                            }

                            fsData.Seek(offset, SeekOrigin.Begin);

                            var tileCount = length / 7;

                            if (_tileBuffer.Length < tileCount)
                            {
                                _tileBuffer = new StaticTile[tileCount];
                            }

                            var staTiles = _tileBuffer;

                            fsData.ReadExactly(MemoryMarshal.AsBytes(staTiles.AsSpan(0, tileCount)));

                            for (var j = 0; j < tileCount; ++j)
                            {
                                var cur = staTiles[j];
                                lists[cur.X & 0x7][cur.Y & 0x7].Add(Art.GetLegalItemId(cur.Id), cur.Hue, cur.Z);
                            }

                            var tiles = new HuedTile[8][][];

                            for (var x = 0; x < 8; ++x)
                            {
                                tiles[x] = new HuedTile[8][];

                                for (var y = 0; y < 8; ++y)
                                {
                                    tiles[x][y] = lists[x][y].ToArray();
                                }
                            }

                            if (StaticBlocks[blockX] == null)
                            {
                                StaticBlocks[blockX] = new HuedTile[matrix.BlockHeight][][][];
                            }

                            StaticBlocks[blockX][blockY] = tiles;
                        }

                        return count;
                    }
                }
            }
        }
    }
}
