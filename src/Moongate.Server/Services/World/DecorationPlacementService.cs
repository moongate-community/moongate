using Moongate.Core.Geometry;
using Moongate.Persistence.Entities;
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
    public (int Placed, int Skipped, int Converted) Place()
    {
        EnsureTemplate();

        var placed = 0;
        var skipped = 0;
        var converted = 0;

        foreach (var placement in Everything())
        {
            if (ExistingAt(placement.MapId, placement.Point, placement.ItemId) is { } existing)
            {
                if (Convert(existing, placement.TemplateId))
                {
                    converted++;
                }
                else
                {
                    skipped++;
                }

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

        _logger.Information(
            "Decoration: placed {Placed}, skipped {Skipped} already present, converted {Converted}",
            placed,
            skipped,
            converted
        );

        return (placed, skipped, converted);
    }

    /// <summary>
    /// Gives an object already standing there the template it should have been built from, and returns
    /// whether it needed it.
    /// <para>
    /// This exists because of a shard that was decorated before doors could open: 944 of them are
    /// built from the inert template, and the idempotence check would leave them that way forever —
    /// it sees the right graphic at the right point and moves on. So the check asks a second question,
    /// is it here as the right kind of thing, and repairs it where the answer is no.
    /// </para>
    /// <para>
    /// The item keeps its serial, so nothing that references it breaks. It is idempotent for the same
    /// reason placement is: a second run finds the template already correct and converts nothing.
    /// </para>
    /// </summary>
    private bool Convert(ItemEntity existing, string templateId)
    {
        if (existing.TemplateId == templateId || _templates.GetById(templateId) is not { } template)
        {
            return false;
        }

        existing.TemplateId = templateId;
        existing.ScriptId = template.ScriptId;

        // Save publishes ItemChangedEvent, so anyone standing there sees the door become a door.
        _items.Save(existing);

        return true;
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
            yield return new(
                placement.MapId,
                placement.Point,
                placement.ItemId,
                placement.Hue,
                0,
                "",
                TemplateFor(placement.Type)
            );
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

    /// <summary>
    /// The object already standing on that tile with that graphic, or null. Keyed on graphic, map and
    /// point rather than on anything the object says about itself, so a corrected name or template
    /// still finds the same object instead of placing a second one beside it.
    /// </summary>
    private ItemEntity? ExistingAt(int mapId, Point3D point, int itemId)
        => _spatial.GetItemsInRange(mapId, point, 0)
                   .FirstOrDefault(item => item.ItemId == itemId && item.Position == point);

    /// <summary>
    /// The template a declared object is built from: its own where that gives it behaviour, the inert
    /// decoration one otherwise.
    /// <para>
    /// Falling back when a door's template is not registered is deliberate. A door that does not open
    /// is worse than furniture, but a hole where a door should be is worse than both — and a shard
    /// that has curated its own template set is entitled to be missing one.
    /// </para>
    /// </summary>
    private string TemplateFor(string declaredType)
        => DoorTemplates.For(declaredType) is { } door && _templates.GetById(door) is not null
               ? door
               : TemplateId;

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
