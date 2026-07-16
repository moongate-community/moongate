using System.Runtime.InteropServices;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Helpers;
using Moongate.Ultima.Io;

namespace Moongate.Ultima.Maps;

public sealed class TileMatrix : IDisposable
{
    private readonly HuedTile[][][][][] _staticTiles;
    private readonly Tile[][][] _landTiles;
    private bool[][] _removedStaticBlock;
    private List<StaticTile>[][] _staticTilesToAdd;

    public static Tile[] InvalidLandBlock { get; private set; }
    public static HuedTile[][][] EmptyStaticBlock { get; private set; }

    private FileStream _map;
    private BinaryReader _uopReader;
    private FileStream _statics;
    private Entry3D[] _staticIndex;

    public bool StaticIndexInit { get; set; }

    public TileMatrixPatch Patch { get; }

    public int BlockWidth { get; }

    public int BlockHeight { get; }

    public int Width { get; }

    public int Height { get; }

    private readonly string _mapPath;
    private readonly string _indexPath;
    private readonly string _staticsPath;

    public TileMatrix(int fileIndex, int mapId, int width, int height, string path)
    {
        Width = width;
        Height = height;
        BlockWidth = width >> 3;
        BlockHeight = height >> 3;

        if (path == null)
        {
            _mapPath = Files.GetFilePath($"map{fileIndex}.mul");

            if (string.IsNullOrEmpty(_mapPath) || !File.Exists(_mapPath))
            {
                _mapPath = Files.GetFilePath($"map{fileIndex}LegacyMUL.uop");
            }

            if (_mapPath?.EndsWith(".uop") == true)
            {
                IsUOPFormat = true;
            }
        }
        else
        {
            _mapPath = Path.Combine(path, $"map{fileIndex}.mul");

            if (!File.Exists(_mapPath))
            {
                _mapPath = Path.Combine(path, $"map{fileIndex}LegacyMUL.uop");
            }

            if (!File.Exists(_mapPath))
            {
                _mapPath = null;
            }
            else if (_mapPath?.EndsWith(".uop") == true)
            {
                IsUOPFormat = true;
            }
        }

        if (path == null)
        {
            _indexPath = Files.GetFilePath($"staidx{fileIndex}.mul");
        }
        else
        {
            _indexPath = Path.Combine(path, $"staidx{fileIndex}.mul");

            if (!File.Exists(_indexPath))
            {
                _indexPath = null;
            }
        }

        if (path == null)
        {
            _staticsPath = Files.GetFilePath($"statics{fileIndex}.mul");
        }
        else
        {
            _staticsPath = Path.Combine(path, $"statics{fileIndex}.mul");

            if (!File.Exists(_staticsPath))
            {
                _staticsPath = null;
            }
        }

        EmptyStaticBlock = new HuedTile[8][][];

        for (var i = 0; i < 8; ++i)
        {
            EmptyStaticBlock[i] = new HuedTile[8][];

            for (var j = 0; j < 8; ++j)
            {
                EmptyStaticBlock[i][j] = Array.Empty<HuedTile>();
            }
        }

        InvalidLandBlock = new Tile[196];

        _landTiles = new Tile[BlockWidth][][];
        _staticTiles = new HuedTile[BlockWidth][][][][];

        Patch = new(this, mapId, path);
    }

    private static HuedTileList[][] _lists;
    private static byte[] _buffer;

    /*
     * UOP map files support code, written by Wyatt (c) www.ruosi.org
     * It's not possible if some entry has unknown hash. Thrown exception
     * means that EA changed maps UOPs again.
     */
    public bool IsUOPFormat { get; set; }
    public bool IsUOPAlreadyRead { get; set; }

    private readonly struct UopFile
    {
        public readonly long Offset;
        public readonly int Length;

        public UopFile(long offset, int length)
        {
            Offset = offset;
            Length = length;
        }
    }

    private UopFile[] UOPFiles { get; set; }
    private long UOPLength => _map.Length;

    public void AddPendingStatic(int blockX, int blockY, StaticTile toAdd)
    {
        if (_staticTilesToAdd == null)
        {
            _staticTilesToAdd = new List<StaticTile>[BlockHeight][];
        }

        if (_staticTilesToAdd[blockY] == null)
        {
            _staticTilesToAdd[blockY] = new List<StaticTile>[BlockWidth];
        }

        if (_staticTilesToAdd[blockY][blockX] == null)
        {
            _staticTilesToAdd[blockY][blockX] = new();
        }

        _staticTilesToAdd[blockY][blockX].Add(toAdd);
    }

    public bool AllFilesExist()
        => _mapPath != null && _indexPath != null && _staticsPath != null;

    public void CloseStreams()
    {
        _map?.Close();
        _uopReader?.Close();
        _statics?.Close();
    }

    public void Dispose()
        => CloseStreams();

    public Tile[] GetLandBlock(int x, int y, bool patch = true)
    {
        if (x < 0 || y < 0 || x >= BlockWidth || y >= BlockHeight)
        {
            return InvalidLandBlock;
        }

        if (_landTiles[x] == null)
        {
            _landTiles[x] = new Tile[BlockHeight][];
        }

        var tiles = _landTiles[x][y] ?? (_landTiles[x][y] = ReadLandBlock(x, y));

        if (Map.UseDiff && patch && Patch.LandBlocksCount > 0 && Patch.LandBlocks[x]?[y] != null)
        {
            tiles = Patch.LandBlocks[x][y];
        }

        return tiles;
    }

    public Tile GetLandTile(int x, int y, bool patch)
        => GetLandBlock(x >> 3, y >> 3, patch)[((y & 0x7) << 3) + (x & 0x7)];

    public Tile GetLandTile(int x, int y)
        => GetLandBlock(x >> 3, y >> 3)[((y & 0x7) << 3) + (x & 0x7)];

    public StaticTile[] GetPendingStatics(int blockX, int blockY)
    {
        if (_staticTilesToAdd?[blockY] == null)
        {
            return null;
        }

        if (_staticTilesToAdd[blockY][blockX] == null)
        {
            return null;
        }

        return _staticTilesToAdd[blockY][blockX].ToArray();
    }

    // TODO: unused?
    //public void SetStaticBlock(int x, int y, HuedTile[][][] value)
    //{
    //    if (x < 0 || y < 0 || x >= BlockWidth || y >= BlockHeight)
    //    {
    //        return;
    //    }

    //    if (_staticTiles[x] == null)
    //    {
    //        _staticTiles[x] = new HuedTile[BlockHeight][][][];
    //    }

    //    _staticTiles[x][y] = value;
    //}

    public HuedTile[][][] GetStaticBlock(int x, int y, bool patch = true)
    {
        if (x < 0 || y < 0 || x >= BlockWidth || y >= BlockHeight)
        {
            return EmptyStaticBlock;
        }

        if (_staticTiles[x] == null)
        {
            _staticTiles[x] = new HuedTile[BlockHeight][][][];
        }

        HuedTile[][][] tiles = _staticTiles[x][y] ?? (_staticTiles[x][y] = ReadStaticBlock(x, y));

        if (Map.UseDiff && patch && Patch.StaticBlocksCount > 0 && Patch.StaticBlocks[x]?[y] != null)
        {
            tiles = Patch.StaticBlocks[x][y];
        }

        return tiles;
    }

    public HuedTile[] GetStaticTiles(int x, int y, bool patch)
        => GetStaticBlock(x >> 3, y >> 3, patch)[x & 0x7][y & 0x7];

    public HuedTile[] GetStaticTiles(int x, int y)
        => GetStaticBlock(x >> 3, y >> 3)[x & 0x7][y & 0x7];

    public bool IsStaticBlockRemoved(int blockX, int blockY)
    {
        if (_removedStaticBlock?[blockX] == null)
        {
            return false;
        }

        return _removedStaticBlock[blockX][blockY];
    }

    public bool PendingStatic(int blockX, int blockY)
    {
        if (_staticTilesToAdd?[blockY] == null)
        {
            return false;
        }

        if (_staticTilesToAdd[blockY][blockX] == null)
        {
            return false;
        }

        return true;
    }

    public void RemoveStaticBlock(int blockX, int blockY)
    {
        if (_removedStaticBlock == null)
        {
            _removedStaticBlock = new bool[BlockWidth][];
        }

        if (_removedStaticBlock[blockX] == null)
        {
            _removedStaticBlock[blockX] = new bool[BlockHeight];
        }

        _removedStaticBlock[blockX][blockY] = true;

        if (_staticTiles[blockX] == null)
        {
            _staticTiles[blockX] = new HuedTile[BlockHeight][][][];
        }

        _staticTiles[blockX][blockY] = EmptyStaticBlock;
    }

    public void SetLandBlock(int x, int y, Tile[] value)
    {
        if (x < 0 || y < 0 || x >= BlockWidth || y >= BlockHeight)
        {
            return;
        }

        if (_landTiles[x] == null)
        {
            _landTiles[x] = new Tile[BlockHeight][];
        }

        _landTiles[x][y] = value;
    }

    private long CalculateOffsetFromUOP(long offset)
    {
        long pos = 0;

        foreach (var t in UOPFiles)
        {
            var currentPosition = pos + t.Length;

            if (offset < currentPosition)
            {
                return t.Offset + (offset - pos);
            }

            pos = currentPosition;
        }

        return UOPLength;
    }

    private void InitStatics()
    {
        _staticIndex = new Entry3D[BlockHeight * BlockWidth];

        if (_indexPath == null)
        {
            return;
        }

        using (var index = new FileStream(_indexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            _statics = new(_staticsPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var readLen = (int)Math.Min(index.Length, (long)BlockHeight * BlockWidth * 12);
            index.ReadExactly(MemoryMarshal.AsBytes(_staticIndex.AsSpan()).Slice(0, readLen));

            for (var i = (int)Math.Min(index.Length, BlockHeight * BlockWidth); i < BlockHeight * BlockWidth; ++i)
            {
                _staticIndex[i].Lookup = -1;
                _staticIndex[i].Length = -1;
                _staticIndex[i].Extra = -1;
            }

            StaticIndexInit = true;
        }
    }

    private Tile[] ReadLandBlock(int x, int y)
    {
        if (_map?.CanRead != true || !_map.CanSeek)
        {
            _map = _mapPath == null
                ? null
                : new FileStream(_mapPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (IsUOPFormat && _mapPath != null && !IsUOPAlreadyRead)
            {
                var fi = new FileInfo(_mapPath);
                var uopPattern = fi.Name.Replace(fi.Extension, "").ToLowerInvariant();

                ReadUOPFiles(uopPattern);
                IsUOPAlreadyRead = true;
            }
        }

        var tiles = new Tile[64];

        if (_map == null)
        {
            return tiles;
        }

        long offset = (x * BlockHeight + y) * 196 + 4;

        if (IsUOPFormat)
        {
            offset = CalculateOffsetFromUOP(offset);
        }

        _map.Seek(offset, SeekOrigin.Begin);

        _map.ReadExactly(MemoryMarshal.AsBytes(tiles.AsSpan()));

        return tiles;
    }

    private unsafe HuedTile[][][] ReadStaticBlock(int x, int y)
    {
        if (!StaticIndexInit)
        {
            InitStatics();
        }

        if (_statics?.CanRead != true || !_statics.CanSeek)
        {
            _statics = _staticsPath == null
                ? null
                : new FileStream(_staticsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        if (_statics == null)
        {
            return EmptyStaticBlock;
        }

        var lookup = _staticIndex[x * BlockHeight + y].Lookup;
        var length = _staticIndex[x * BlockHeight + y].Length;

        if (lookup < 0 || length <= 0)
        {
            return EmptyStaticBlock;
        }

        var count = length / 7;

        _statics.Seek(lookup, SeekOrigin.Begin);

        if (_buffer == null || _buffer.Length < length)
        {
            _buffer = new byte[length];
        }

        _statics.ReadExactly(_buffer, 0, length);

        if (_lists == null)
        {
            _lists = new HuedTileList[8][];

            for (var i = 0; i < 8; ++i)
            {
                _lists[i] = new HuedTileList[8];

                for (var j = 0; j < 8; ++j)
                {
                    _lists[i][j] = new();
                }
            }
        }

        var lists = _lists;

        ReadOnlySpan<StaticTile> staticTiles = MemoryMarshal.Cast<byte, StaticTile>(
            _buffer.AsSpan(0, count * sizeof(StaticTile))
        );

        for (var i = 0; i < count; ++i)
        {
            var cur = staticTiles[i];
            lists[cur.X & 0x7][cur.Y & 0x7].Add(Art.GetLegalItemId(cur.Id), cur.Hue, cur.Z);
        }

        var tiles = new HuedTile[8][][];

        for (var i = 0; i < 8; ++i)
        {
            tiles[i] = new HuedTile[8][];

            for (var j = 0; j < 8; ++j)
            {
                tiles[i][j] = lists[i][j].ToArray();
            }
        }

        return tiles;
    }

    private void ReadUOPFiles(string pattern)
    {
        _uopReader = new(_map);

        _uopReader.BaseStream.Seek(0, SeekOrigin.Begin);

        if (_uopReader.ReadInt32() != 0x50594D)
        {
            throw new ArgumentException("Bad UOP file.");
        }

        _uopReader.ReadInt64(); // version + signature
        var nextBlock = _uopReader.ReadInt64();
        _uopReader.ReadInt32(); // block capacity
        var count = _uopReader.ReadInt32();

        UOPFiles = new UopFile[count];

        var hashes = new Dictionary<ulong, int>();

        for (var i = 0; i < count; i++)
        {
            var file = $"build/{pattern}/{i:D8}.dat";
            var hash = UopUtils.HashFileName(file);

            hashes.TryAdd(hash, i);
        }

        _uopReader.BaseStream.Seek(nextBlock, SeekOrigin.Begin);

        do
        {
            var filesCount = _uopReader.ReadInt32();
            nextBlock = _uopReader.ReadInt64();

            for (var i = 0; i < filesCount; i++)
            {
                var offset = _uopReader.ReadInt64();
                var headerLength = _uopReader.ReadInt32();
                var compressedLength = _uopReader.ReadInt32();
                var decompressedLength = _uopReader.ReadInt32();
                var hash = _uopReader.ReadUInt64();
                _uopReader.ReadUInt32(); // Adler32
                var flag = _uopReader.ReadInt16();

                var length = flag == 1 ? compressedLength : decompressedLength;

                if (offset == 0)
                {
                    continue;
                }

                if (hashes.TryGetValue(hash, out var idx))
                {
                    if (idx < 0 || idx > UOPFiles.Length)
                    {
                        throw new IndexOutOfRangeException(
                            "hashes dictionary and files collection have different count of entries!"
                        );
                    }

                    UOPFiles[idx] = new(offset + headerLength, length);
                }
                else
                {
                    throw new ArgumentException(
                        $"File with hash 0x{hash:X8} was not found in hashes dictionary! EA Mythic changed UOP format!"
                    );
                }
            }
        } while (_uopReader.BaseStream.Seek(nextBlock, SeekOrigin.Begin) != 0);
    }
}
