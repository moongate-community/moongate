using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Maps;

/// <summary>
///     One in-memory Felucca map: land id 3 at Z 0 everywhere unless set, no statics unless added.
/// </summary>
public sealed class FakeMapService : IMapService
{
    private readonly int _width;
    private readonly int _height;
    private readonly ushort[,] _landIds;
    private readonly sbyte[,] _landZ;
    private readonly Dictionary<(int X, int Y), MapStaticTile[]> _statics = new();

    public IReadOnlyList<MapType> Maps { get; } = [MapType.Felucca];

    public FakeMapService(int width, int height)
    {
        _width = width;
        _height = height;
        _landIds = new ushort[width, height];
        _landZ = new sbyte[width, height];
        SetLandId(0, 0, width - 1, height - 1, 3);
    }

    public FakeMapService SetLand(int x, int y, ushort id, sbyte z)
    {
        _landIds[x, y] = id;
        _landZ[x, y] = z;

        return this;
    }

    public FakeMapService SetLandZ(int x0, int y0, int x1, int y1, sbyte z)
    {
        for (var x = x0; x <= x1; x++)
        {
            for (var y = y0; y <= y1; y++)
            {
                _landZ[x, y] = z;
            }
        }

        return this;
    }

    public FakeMapService SetLandId(int x0, int y0, int x1, int y1, ushort id)
    {
        for (var x = x0; x <= x1; x++)
        {
            for (var y = y0; y <= y1; y++)
            {
                _landIds[x, y] = id;
            }
        }

        return this;
    }

    public FakeMapService AddStatic(int x, int y, ushort id, sbyte z)
    {
        // Keep one array per cell, as a map block cache would, so reads allocate nothing.
        var list = _statics.TryGetValue((x, y), out var existing) ? existing : [];
        _statics[(x, y)] = [.. list, new() { Id = id, Z = z }];

        return this;
    }

    public bool Contains(MapType map, int x, int y)
    {
        return map == MapType.Felucca && (uint)x < (uint)_width && (uint)y < (uint)_height;
    }

    public MapLandTile GetLand(MapType map, int x, int y)
    {
        Check(map, x, y);

        return new() { Id = _landIds[x, y], Z = _landZ[x, y] };
    }

    public IReadOnlyList<MapStaticTile> GetStatics(MapType map, int x, int y)
    {
        Check(map, x, y);

        return _statics.TryGetValue((x, y), out var list) ? list : [];
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    private void Check(MapType map, int x, int y)
    {
        if (map != MapType.Felucca)
        {
            throw new KeyNotFoundException($"Map {map} is not loaded.");
        }

        if (!Contains(map, x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside {map}.");
        }
    }
}
