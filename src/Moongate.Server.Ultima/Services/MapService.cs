using System.Collections.Frozen;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Maps;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Opens one <see cref="TileMatrix" /> per map of <c>data/maps.toml</c>; it runs after the data loaders, which read
///     that file, and after <c>IUltimaDataService</c>, which points the client-file readers at the client directory.
/// </summary>
public class MapService : IMapService
{
    private readonly ILogger _logger = Log.ForContext<MapService>();
    private readonly IDataLoaderService _dataLoaderService;

    private FrozenDictionary<MapType, TileMatrix> _matrices = FrozenDictionary<MapType, TileMatrix>.Empty;

    public IReadOnlyList<MapType> Maps { get; private set; } = [];

    public MapService(IDataLoaderService dataLoaderService)
    {
        _dataLoaderService = dataLoaderService;
    }

    public Task StartAsync()
    {
        var maps = _dataLoaderService.GetEntities<MapContent>();
        var matrices = new Dictionary<MapType, TileMatrix>();

        try
        {
            foreach (var map in maps)
            {
                var matrix = new TileMatrix(map.FileIndex, (int)map.Map, map.Size.X, map.Size.Y, null!);
                matrices.Add(map.Map, matrix);

                if (!matrix.AllFilesExist())
                {
                    throw new FileNotFoundException(
                        $"Map {map.Name} needs map{map.FileIndex}.mul or map{map.FileIndex}LegacyMUL.uop, " +
                        $"staidx{map.FileIndex}.mul and statics{map.FileIndex}.mul in the Ultima path. " +
                        "Remove the map from data/maps.toml if the client has no files for it."
                    );
                }
            }
        }
        catch
        {
            foreach (var matrix in matrices.Values)
            {
                matrix.Dispose();
            }

            throw;
        }

        _matrices = matrices.ToFrozenDictionary();
        Maps = maps.Select(map => map.Map).ToArray();
        _logger.Information("Opened {Count} maps: {Maps}", Maps.Count, Maps);

        return Task.CompletedTask;
    }

    public bool Contains(MapType map, int x, int y)
    {
        return _matrices.TryGetValue(map, out var matrix) &&
               (uint)x < (uint)matrix.Width &&
               (uint)y < (uint)matrix.Height;
    }

    public MapLandTile GetLand(MapType map, int x, int y)
    {
        var tile = GetMatrix(map, x, y).GetLandTile(x, y);

        return new() { Id = tile.Id, Z = tile.Z };
    }

    public IReadOnlyList<MapStaticTile> GetStatics(MapType map, int x, int y)
    {
        var tiles = GetMatrix(map, x, y).GetStaticTiles(x, y);

        if (tiles.Length == 0)
        {
            return [];
        }

        var statics = new MapStaticTile[tiles.Length];

        for (var i = 0; i < tiles.Length; i++)
        {
            statics[i] = new() { Id = tiles[i].Id, Z = tiles[i].Z, Hue = tiles[i].Hue };
        }

        return statics;
    }

    private TileMatrix GetMatrix(MapType map, int x, int y)
    {
        if (!_matrices.TryGetValue(map, out var matrix))
        {
            throw new KeyNotFoundException($"Map {map} is not loaded.");
        }

        if ((uint)x >= (uint)matrix.Width || (uint)y >= (uint)matrix.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"({x}, {y}) is outside {map}, which is {matrix.Width}x{matrix.Height}."
            );
        }

        return matrix;
    }

    public Task StopAsync()
    {
        foreach (var matrix in _matrices.Values)
        {
            matrix.Dispose();
        }

        _matrices = FrozenDictionary<MapType, TileMatrix>.Empty;
        Maps = [];

        return Task.CompletedTask;
    }
}
