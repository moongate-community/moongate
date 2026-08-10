using Moongate.Core.Geometry;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Items;
using Moongate.UO.Data.Signs;
using Moongate.UO.Data.World;
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
    private readonly IItemTemplateService _templates;
    private readonly ISignService _signs;

    public DecorationPlacementService(
        IDecorationCatalog decorations,
        IItemFactoryService factory,
        IItemService items,
        ISpatialIndexService spatial,
        IItemTemplateService templates,
        ISignService signs
    )
    {
        _decorations = decorations;
        _factory = factory;
        _items = items;
        _spatial = spatial;
        _templates = templates;
        _signs = signs;
    }

    /// <summary>
    /// Places everything the catalogue holds that is not already standing there, and reports both
    /// numbers. A command that places forty thousand objects in silence is one you cannot tell
    /// succeeded from one that did nothing.
    /// </summary>
    public (int Placed, int Skipped) Place()
    {
        EnsureTemplate();

        var placed = 0;
        var skipped = 0;

        foreach (var placement in Everything())
        {
            if (AlreadyThere(placement.MapId, placement.Point, placement.ItemId))
            {
                skipped++;

                continue;
            }

            var created = _factory.CreateFromTemplate(placement.TemplateId, 1, 1, new Hue((ushort)placement.Hue));

            if (created.Count == 0)
            {
                // The template is missing, which means every placement will fail the same way.
                _logger.Error(
                    "Template {Template} is not registered; no decoration can be placed",
                    placement.TemplateId
                );

                break;
            }

            // The instance carries its own appearance: one template, 2381 graphics.
            var item = created[0];

            item.ItemId = placement.ItemId;
            item.NameCliloc = placement.NameCliloc;
            item.Name = placement.Name;
            _items.MoveToWorld(item, placement.MapId, placement.Point);
            placed++;
        }

        _logger.Information("Decoration: placed {Placed}, skipped {Skipped} already present", placed, skipped);

        return (placed, skipped);
    }

    /// <summary>
    /// Every object the world should hold, from both sources, in one sequence.
    /// <para>
    /// Signs are kept in their own registry rather than converted into decoration groups — they are
    /// already loaded, already keyed by map, and converting them would buy nothing. What the two do
    /// share is the only thing that matters here: becoming an item at a point, once. Flattening them
    /// into one sequence is what lets a single loop own the counting, so "placed" and "already there"
    /// cannot both be true of one object.
    /// </para>
    /// </summary>
    private IEnumerable<WorldPlacement> Everything()
    {
        foreach (var placement in _decorations.All)
        {
            yield return new(placement.MapId, placement.Point, placement.ItemId, placement.Hue, 0, "", TemplateId);
        }

        foreach (var sign in _signs.All)
        {
            var (cliloc, text) = SignLabel.Split(sign.Label);

            yield return new(
                (int)sign.Map,
                new(sign.X, sign.Y, sign.Z),
                sign.ItemId,
                0,
                cliloc,
                text,
                TemplateId
            );
        }
    }

    private bool AlreadyThere(int mapId, Moongate.Core.Geometry.Point3D point, int itemId)
        => _spatial.GetItemsInRange(mapId, point, 0)
                   .Any(item => item.ItemId == itemId && item.Position == point);

    /// <summary>
    /// Registers the decoration template when the registry has none.
    /// <para>
    /// It ships as YAML under Assets, but a shard whose <c>templates/items/</c> directory already
    /// existed never received it: <see cref="Loaders.ItemTemplatesLoader" /> seeds that directory only
    /// when it is absent, on purpose, so an operator's curated template set is never written into.
    /// That contract is right and stays — but it means every template added after a shard's first boot
    /// is invisible to it, and decoration would be dead on every existing shard.
    /// </para>
    /// <para>
    /// So the template is treated as what it is: an implementation detail of this service rather than
    /// content anyone curates. A YAML that is present wins, because this only fills a gap.
    /// </para>
    /// </summary>
    private void EnsureTemplate()
    {
        if (_templates.GetById(TemplateId) is not null)
        {
            return;
        }

        _templates.Register(
            new ItemTemplate
            {
                Id = TemplateId,
                Name = "",
                Category = "World",
                Description = "Scenery placed by world decoration. One template for every appearance -- " +
                              "each instance carries its own graphic and hue.",
                ItemId = 1,
                IsMovable = false,
                Tags = ["decoration"]
            }
        );

        _logger.Information(
            "Registered the built-in {Template} template: this shard's item templates do not ship one",
            TemplateId
        );
    }
}
