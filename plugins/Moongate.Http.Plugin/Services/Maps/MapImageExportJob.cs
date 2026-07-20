using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Interfaces.Maps;
using Moongate.Http.Plugin.Types;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Http.Plugin.Services.Maps;

/// <summary>
/// Builds every facet's tiles and whole-facet image ahead of anyone asking. It goes through
/// <see cref="IMapImageService" /> like any request, so it takes the same gate per tile and interleaves
/// with live traffic rather than holding Ultima for the length of the run.
/// </summary>
public sealed class MapImageExportJob : IMapImageExportJob
{
    /// <summary>Stands for the facet's whole image in the plan, which has no zoom of its own.</summary>
    private const int FullImageZoom = -1;

    private readonly ILogger _logger = Log.ForContext<MapImageExportJob>();
    private readonly IMapImageService _maps;
    private readonly IUltimaMapProvider _provider;
    private readonly object _sync = new();

    private MapImageExportStateType _state = MapImageExportStateType.Idle;
    private int _done;
    private int _total;
    private int _failed;
    private DateTimeOffset? _startedAt;

    public MapImageExportJob(IMapImageService maps, IUltimaMapProvider provider)
    {
        _maps = maps;
        _provider = provider;
    }

    public MapImageExportStatus Status
    {
        get
        {
            lock (_sync)
            {
                return new(_state.ToString(), _done, _total, _failed, _startedAt);
            }
        }
    }

    public bool TryStart()
    {
        lock (_sync)
        {
            if (_state == MapImageExportStateType.Running)
            {
                return false;
            }

            _state = MapImageExportStateType.Running;
            _done = 0;
            _total = 0;
            _failed = 0;
            _startedAt = DateTimeOffset.UtcNow;
        }

        // Deliberately not awaited: the route answers 202 and the caller polls. RunAsync catches everything
        // and records it in the state, so nothing is left to an unobserved exception.
        _ = Task.Run(RunAsync);

        return true;
    }

    /// <summary>
    /// Every tile, deepest zoom first, then the whole-facet image. Bottom-up on purpose: each level
    /// composes from children already on disk, so nothing recurses and no tile is built twice.
    /// </summary>
    private List<(MapType Facet, int Zoom, int X, int Y)> Plan()
    {
        var work = new List<(MapType, int, int, int)>();

        foreach (var facet in _provider.Facets)
        {
            if (_provider.Get(facet) is not { } map)
            {
                continue;
            }

            var maxZoom = _maps.MaxZoomFor(facet);

            for (var zoom = maxZoom; zoom >= 0; zoom--)
            {
                var across = MapTileGeometry.TilesAcross(map.Width, zoom, maxZoom);
                var down = MapTileGeometry.TilesDown(map.Height, zoom, maxZoom);

                for (var x = 0; x < across; x++)
                {
                    for (var y = 0; y < down; y++)
                    {
                        work.Add((facet, zoom, x, y));
                    }
                }
            }

            work.Add((facet, FullImageZoom, 0, 0));
        }

        return work;
    }

    private async Task RunAsync()
    {
        try
        {
            var work = Plan();

            lock (_sync)
            {
                _total = work.Count;
            }

            foreach (var (facet, zoom, x, y) in work)
            {
                try
                {
                    if (zoom == FullImageZoom)
                    {
                        await _maps.GetFullAsync(facet, MapRenderStyleType.Flat);
                    }
                    else
                    {
                        await _maps.GetTileAsync(facet, MapRenderStyleType.Flat, zoom, x, y);
                    }

                    lock (_sync)
                    {
                        _done++;
                    }
                }
                catch (Exception exception)
                {
                    // One unreadable tile must not end the run: the point is to warm what can be warmed.
                    _logger.Warning(exception, "Could not export {Facet} z{Zoom} {X},{Y}", facet, zoom, x, y);

                    lock (_sync)
                    {
                        _failed++;
                    }
                }
            }

            lock (_sync)
            {
                _state = MapImageExportStateType.Completed;
            }

            _logger.Information("Map image export finished: {Done} produced, {Failed} failed", _done, _failed);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Map image export stopped early");

            lock (_sync)
            {
                _state = MapImageExportStateType.Failed;
            }
        }
    }
}
