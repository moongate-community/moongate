using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Places the decoration files as ModernUO's <c>[Decorate</c> does, file by file: the items are built and saved off
///     the game loop in one transaction per file, which gives them their serials, then enter the world on the loop.
/// </summary>
public sealed class DecorationService : IDecorationService
{
    public const string DecorationTemplate = "decoration";
    public const string DoorTemplate = "decoration_door";
    public const string TypeProp = "decoration_type";
    public const string LinkProp = "door.link";
    public const string OutsideTheMap = "outside the map";

    private readonly ILogger _logger = Log.ForContext<DecorationService>();
    private readonly IDecorationsLoader _loader;
    private readonly IItemFactoryService _factory;
    private readonly IItemService _items;
    private readonly ISectorService _sectors;
    private readonly IWorldViewService _view;
    private readonly IGameLoopService _loop;

    public DecorationService(
        IDecorationsLoader loader,
        IItemFactoryService factory,
        IItemService items,
        ISectorService sectors,
        IWorldViewService view,
        IGameLoopService loop
    )
    {
        _loader = loader;
        _factory = factory;
        _items = items;
        _sectors = sectors;
        _view = view;
        _loop = loop;
    }

    public async Task<DecorationResult> DecorateAsync(
        IProgress<DecorationFileResult>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var files = await _loader.LoadAsync(cancellationToken);
        var placed = 0;
        var present = 0;
        var skipped = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await DecorateFileAsync(file, cancellationToken);
            placed += result.Placed;
            present += result.Present;
            skipped += result.Skipped;

            _logger.Information(
                "Decorating {Folder}/{Name}: {Placed} placed, {Present} already there, {Skipped} skipped {SkippedByType}",
                result.Folder,
                result.Name,
                result.Placed,
                result.Present,
                result.Skipped,
                result.SkippedByType
            );
            progress?.Report(result);
        }

        _logger.Information(
            "Decoration done: {Placed} placed, {Present} already there, {Skipped} skipped in {Files} files",
            placed,
            present,
            skipped,
            files.Count
        );

        return new(placed, present, skipped, files.Count);
    }

    private async Task<DecorationFileResult> DecorateFileAsync(DecorationFile file, CancellationToken cancellationToken)
    {
        var skipped = new Dictionary<string, int>(StringComparer.Ordinal);
        var candidates = new List<(DecorationBlock Block, MapType Map, Point3D Location)>();

        foreach (var block in file.Blocks)
        {
            if (block.ItemId is null || IsSkipped(block.Type))
            {
                Count(skipped, block.Type, block.Locations.Count * file.Maps.Count);

                continue;
            }

            foreach (var map in file.Maps)
            {
                candidates.AddRange(block.Locations.Select(location => (block, map, location)));
            }
        }

        // What is on the ground is read on the loop; the new items are built and saved off it.
        var free = new List<(DecorationBlock Block, MapType Map, Point3D Location)>();
        var present = 0;
        await OnLoopAsync(
            () =>
            {
                var seen = new HashSet<(MapType, Point3D, int)>();

                foreach (var candidate in candidates)
                {
                    var (block, map, location) = candidate;

                    if (!_sectors.IsInside(map, location.X, location.Y))
                    {
                        Count(skipped, OutsideTheMap, 1);
                    }
                    else if (!seen.Add((map, location, block.ItemId!.Value)) || IsThere(map, location, block.ItemId.Value))
                    {
                        present++;
                    }
                    else
                    {
                        free.Add(candidate);
                    }
                }
            }
        );

        var items = free.Select(candidate => Build(candidate.Block, candidate.Map, candidate.Location)).ToList();

        if (items.Count > 0)
        {
            await _factory.SaveAsync(items, cancellationToken);

            // The links need the serials the save gave; the linked doors are saved again with them.
            var linked = LinkDoors(items);

            if (linked.Count > 0)
            {
                await _factory.SaveAsync(linked, cancellationToken);
            }

            await OnLoopAsync(
                () =>
                {
                    _items.Add(items);

                    foreach (var item in items)
                    {
                        _view.ItemAppeared(item);
                    }
                }
            );
        }

        return new(file.Folder, file.Name, items.Count, present, skipped);
    }

    // Kinds whose behaviour is not written yet: a plain item in their place would look or act wrong.
    private static bool IsSkipped(string type)
    {
        return type is "Spawner" or "MarkContainer" or "PublicMoongate" ||
               type.EndsWith("Teleporter", StringComparison.Ordinal) ||
               type.EndsWith("Addon", StringComparison.Ordinal);
    }

    private static bool IsDoor(string type)
    {
        return type.Contains("Door", StringComparison.Ordinal) || type.Contains("Gate", StringComparison.Ordinal);
    }

    private bool IsThere(MapType map, Point3D location, int graphic)
    {
        return _sectors.GetItemsInRange(map, location, 0)
                       .Any(item => item.ItemId == graphic && item.GroundLocation == location);
    }

    private ItemEntity Build(DecorationBlock block, MapType map, Point3D location)
    {
        var door = IsDoor(block.Type);
        var item = _factory.Create(door ? DoorTemplate : DecorationTemplate);
        var props = new Dictionary<string, object?>(StringComparer.Ordinal);
        item.ItemId = block.ItemId!.Value;

        foreach (var (key, value) in block.Props)
        {
            switch (key, value)
            {
                case ("hue", long hue and >= 0 and <= ushort.MaxValue):
                    item.Hue = new Hue((ushort)hue);

                    break;
                case ("name", string name):
                    item.Name = name;

                    break;
                default:
                    props[key] = value;

                    break;
            }
        }

        if (door)
        {
            props[TypeProp] = block.Type;
        }

        item.Props = props.Count > 0 ? props : null;
        item.PlaceOnGround(map, location);

        return item;
    }

    // Pairs each door with an unpaired door of the same kind next to it, as ModernUO's double doors.
    private static List<ItemEntity> LinkDoors(List<ItemEntity> items)
    {
        var doors = items.Where(item => item.TemplateId == DoorTemplate).ToList();
        var linked = new List<ItemEntity>();

        foreach (var door in doors.Where(door => !door.Props!.ContainsKey(LinkProp)))
        {
            var partner = doors.FirstOrDefault(
                other => other != door && !other.Props!.ContainsKey(LinkProp) && Adjacent(door, other)
            );

            if (partner is null)
            {
                continue;
            }

            door.Props![LinkProp] = (long)partner.Id.Value;
            partner.Props![LinkProp] = (long)door.Id.Value;
            linked.Add(door);
            linked.Add(partner);
        }

        return linked;
    }

    private static bool Adjacent(ItemEntity door, ItemEntity other)
    {
        var dx = Math.Abs(door.X!.Value - other.X!.Value);
        var dy = Math.Abs(door.Y!.Value - other.Y!.Value);

        return door.Map == other.Map &&
               door.Z == other.Z &&
               dx + dy == 1 &&
               Equals(door.Props![TypeProp], other.Props![TypeProp]);
    }

    private static void Count(Dictionary<string, int> skipped, string type, int count)
    {
        skipped[type] = skipped.GetValueOrDefault(type) + count;
    }

    private async Task OnLoopAsync(Action action)
    {
        var work = new LoopActionWorkItem(action);
        await _loop.PostAsync(work, CancellationToken.None);
        await work.Completion;
    }
}
