using Moongate.Core.Geometry;
using Moongate.Core.Interfaces;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.World;
using Serilog;

namespace Moongate.Server.Services.World;

/// <summary>
/// Puts doors in the doorways the map art draws and leaves empty.
/// <para>
/// The reading and the writing happen on different threads on purpose. Finding the doorways means
/// asking 21.4 million tiles what statics they hold, which on the game loop would take the world away
/// from everyone playing for minutes; the map files are read-only, so that runs beside the loop. The
/// items it produces are world state, so those are created on the loop, in batches, where every other
/// world write happens.
/// </para>
/// </summary>
public sealed class DoorGenerationService
{
    /// <summary>
    /// How many doors to create per trip onto the loop. Small enough that a frame's worth of work
    /// stays a frame's worth, large enough not to pay for the hop thousands of times.
    /// </summary>
    private const int BatchSize = 250;

    private readonly ILogger _logger = Log.ForContext<DoorGenerationService>();
    private readonly IItemFactoryService _factory;
    private readonly IItemService _items;
    private readonly ISpatialIndexService _spatial;
    private readonly IItemTemplateService _templates;
    private readonly IGameLoopContext _loop;
    private readonly Func<int, int, int, IReadOnlyList<(int Id, int Z)>> _staticsAt;
    private readonly Func<int, string> _nameOf;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<DoorScanRegion>> _regions;

    public DoorGenerationService(
        IItemFactoryService factory,
        IItemService items,
        ISpatialIndexService spatial,
        IItemTemplateService templates,
        IGameLoopContext loop,
        Func<int, int, int, IReadOnlyList<(int Id, int Z)>> staticsAt,
        Func<int, string> nameOf,
        IReadOnlyDictionary<int, IReadOnlyList<DoorScanRegion>> regions
    )
    {
        _factory = factory;
        _items = items;
        _spatial = spatial;
        _templates = templates;
        _loop = loop;
        _staticsAt = staticsAt;
        _nameOf = nameOf;
        _regions = regions;
    }

    /// <summary>
    /// Scans every mapped region and fills the doorways it finds. Safe to run twice: a doorway that
    /// already holds a door with that graphic is left alone.
    /// </summary>
    public async Task<DoorGenerationResult> GenerateAsync(Action<string>? progress = null)
    {
        var placed = 0;
        var skipped = 0;
        var scanned = 0L;

        foreach (var (mapId, regions) in _regions.OrderBy(entry => entry.Key))
        {
            foreach (var region in regions)
            {
                // Off the loop: this is millions of reads against read-only client files.
                var found = await Task.Run(() => Scan(mapId, region, ref scanned));

                progress?.Invoke($"map {mapId} {region.StartX},{region.StartY}: {found.Count} doorway(s)");

                foreach (var batch in found.Chunk(BatchSize))
                {
                    // On the loop: creating items, touching the spatial index, writing the store.
                    var (batchPlaced, batchSkipped) = await _loop.InvokeAsync(() => Place(batch));

                    placed += batchPlaced;
                    skipped += batchSkipped;
                }
            }
        }

        _logger.Information(
            "Doors: placed {Placed}, skipped {Skipped} already there, from {Scanned} tile(s)",
            placed,
            skipped,
            scanned
        );

        return new(placed, skipped, scanned);
    }

    /// <summary>Creates one batch of doors, skipping the doorways that already hold theirs.</summary>
    private (int Placed, int Skipped) Place(IReadOnlyList<DoorPlacement> batch)
    {
        var placed = 0;
        var skipped = 0;

        foreach (var placement in batch)
        {
            var templateId = DoorMaterials.TemplateFor(_nameOf(placement.FrameId));
            var template = _templates.GetById(templateId) ?? _templates.GetById(DoorMaterials.DefaultTemplateId);

            if (template is null)
            {
                _logger.Error("No door template is registered; no doors can be generated");

                break;
            }

            // The graphic is the facing: closed = base + 2 x facing, which is what the door script
            // reads back to know which way this one hangs.
            var itemId = template.ItemId + (2 * (int)placement.Facing);

            if (AlreadyThere(placement.MapId, placement.Point, itemId))
            {
                skipped++;

                continue;
            }

            var created = _factory.CreateFromTemplate(template.Id, 1, 1, new Hue(0));

            if (created.Count == 0)
            {
                break;
            }

            var door = created[0];

            door.ItemId = itemId;
            _items.MoveToWorld(door, placement.MapId, placement.Point);
            placed++;
        }

        return (placed, skipped);
    }

    private bool AlreadyThere(int mapId, Point3D point, int itemId)
        => _spatial.GetItemsInRange(mapId, point, 0)
                   .Any(item => item.ItemId == itemId && item.Position == point);

    /// <summary>
    /// The doorways in one region. Windows are not doorways, whatever the reference generators do,
    /// and the name the client files give a frame is what says which is which.
    /// </summary>
    private IReadOnlyList<DoorPlacement> Scan(int mapId, DoorScanRegion region, ref long scanned)
    {
        scanned += (long)(region.EndX - region.StartX) * (region.EndY - region.StartY);

        return DoorScan.Scan(
            mapId,
            region,
            (x, y) => _staticsAt(mapId, x, y),
            frameId => DoorMaterials.IsDoorway(_nameOf(frameId))
        );
    }
}
