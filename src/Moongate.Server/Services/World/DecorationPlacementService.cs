using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Hues;
using Serilog;

namespace Moongate.Server.Services.World;

/// <summary>
/// Turns the decoration catalogue into world: persisted items with serials, like any other item.
/// <para>
/// Idempotence is the whole risk here — this runs from a command someone can type twice, and a second
/// run would otherwise double forty thousand objects. It is answered by asking the world rather than
/// by recording that the job was done: an object already standing on that tile with that graphic is
/// left alone. That survives a run interrupted halfway, which a "decorated: true" flag would not.
/// </para>
/// </summary>
public sealed class DecorationPlacementService
{
    /// <summary>The template every decoration instance points at; the graphic differs per instance.</summary>
    public const string TemplateId = "world_decoration";

    private readonly ILogger _logger = Log.ForContext<DecorationPlacementService>();
    private readonly IDecorationCatalog _decorations;
    private readonly IItemFactoryService _factory;
    private readonly IItemService _items;
    private readonly ISpatialIndexService _spatial;

    public DecorationPlacementService(
        IDecorationCatalog decorations,
        IItemFactoryService factory,
        IItemService items,
        ISpatialIndexService spatial
    )
    {
        _decorations = decorations;
        _factory = factory;
        _items = items;
        _spatial = spatial;
    }

    /// <summary>
    /// Places everything the catalogue holds that is not already standing there, and reports both
    /// numbers. A command that places forty thousand objects in silence is one you cannot tell
    /// succeeded from one that did nothing.
    /// </summary>
    public (int Placed, int Skipped) Place()
    {
        var placed = 0;
        var skipped = 0;

        foreach (var placement in _decorations.All)
        {
            if (AlreadyThere(placement.MapId, placement.Point, placement.ItemId))
            {
                skipped++;

                continue;
            }

            var item = _factory.CreateFromTemplate(TemplateId, 1, 1, new Hue((ushort)placement.Hue))
                               .FirstOrDefault();

            if (item is null)
            {
                // The template is missing, which means every placement will fail the same way.
                _logger.Error("Template {Template} is not registered; no decoration can be placed", TemplateId);

                break;
            }

            // The instance carries its own appearance: one template, 2381 graphics.
            item.ItemId = placement.ItemId;
            _items.MoveToWorld(item, placement.MapId, placement.Point);
            placed++;
        }

        _logger.Information("Decoration: placed {Placed}, skipped {Skipped} already present", placed, skipped);

        return (placed, skipped);
    }

    private bool AlreadyThere(int mapId, Moongate.Core.Geometry.Point3D point, int itemId)
        => _spatial.GetItemsInRange(mapId, point, 0)
                   .Any(item => item.ItemId == itemId && item.Position == point);
}
